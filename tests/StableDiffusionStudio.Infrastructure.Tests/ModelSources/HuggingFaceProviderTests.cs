using System.Net;
using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.ModelSources;

namespace StableDiffusionStudio.Infrastructure.Tests.ModelSources;

public class HuggingFaceProviderTests
{
    private readonly IProviderCredentialStore _credentialStore = Substitute.For<IProviderCredentialStore>();

    private HuggingFaceProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var downloadClient = new HttpDownloadClient(httpClient);
        return new HuggingFaceProvider(httpClient, downloadClient, _credentialStore);
    }

    [Fact]
    public async Task SearchAsync_ReturnsModelsFromApi()
    {
        var responseJson = """
        [
            {
                "id": "stabilityai/stable-diffusion-xl-base-1.0",
                "tags": ["diffusers", "sdxl", "text-to-image"],
                "downloads": 500000
            },
            {
                "id": "runwayml/stable-diffusion-v1-5",
                "tags": ["diffusers", "stable-diffusion", "text-to-image"],
                "downloads": 300000
            }
        ]
        """;

        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, responseJson);

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("huggingface", SearchTerm: "stable diffusion");

        var result = await provider.SearchAsync(query);

        result.Models.Should().HaveCount(2);
        result.Models[0].ExternalId.Should().Be("stabilityai/stable-diffusion-xl-base-1.0");
        result.Models[0].Title.Should().Be("stable-diffusion-xl-base-1.0");
        result.Models[0].Family.Should().Be(ModelFamily.SDXL);
        result.Models[0].ProviderUrl.Should().Contain("huggingface.co");
        result.Models[1].ExternalId.Should().Be("runwayml/stable-diffusion-v1-5");
        result.Models[1].Family.Should().Be(ModelFamily.SD15);
    }

    [Fact]
    public async Task SearchAsync_EmptyResult_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, "[]");

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("huggingface", SearchTerm: "nonexistent_model_xyz");

        var result = await provider.SearchAsync(query);

        result.Models.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_ApiError_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.InternalServerError, "error");

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("huggingface", SearchTerm: "test");

        var result = await provider.SearchAsync(query);

        result.Models.Should().BeEmpty();
    }

    [Fact]
    public void Capabilities_CorrectlyDeclared()
    {
        var handler = new MockHttpMessageHandler();
        var provider = CreateProvider(handler);

        provider.Capabilities.CanScanLocal.Should().BeFalse();
        provider.Capabilities.CanSearch.Should().BeTrue();
        provider.Capabilities.CanDownload.Should().BeTrue();
        provider.Capabilities.RequiresAuth.Should().BeFalse();
        provider.Capabilities.SupportedModelTypes.Should().Contain(ModelType.Checkpoint);
        provider.Capabilities.SupportedModelTypes.Should().Contain(ModelType.LoRA);
        provider.Capabilities.SupportedModelTypes.Should().Contain(ModelType.VAE);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_NoToken_ReturnsTrue()
    {
        _credentialStore.GetTokenAsync("huggingface", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var handler = new MockHttpMessageHandler();
        var provider = CreateProvider(handler);

        var result = await provider.ValidateCredentialsAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ValidToken_ReturnsTrue()
    {
        _credentialStore.GetTokenAsync("huggingface", Arg.Any<CancellationToken>())
            .Returns("hf_valid_token");

        var handler = new MockHttpMessageHandler()
            .WithResponse("whoami", HttpStatusCode.OK, """{"name":"testuser"}""");

        var provider = CreateProvider(handler);
        var result = await provider.ValidateCredentialsAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_InvalidToken_ReturnsFalse()
    {
        _credentialStore.GetTokenAsync("huggingface", Arg.Any<CancellationToken>())
            .Returns("hf_invalid_token");

        var handler = new MockHttpMessageHandler()
            .WithResponse("whoami", HttpStatusCode.Unauthorized, "Unauthorized");

        var provider = CreateProvider(handler);
        var result = await provider.ValidateCredentialsAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ScanLocalAsync_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();
        var provider = CreateProvider(handler);

        var result = await provider.ScanLocalAsync(new StorageRoot("/tmp", "Temp"));

        result.Should().BeEmpty();
    }

    [Fact]
    public void ProviderId_IsHuggingface()
    {
        var handler = new MockHttpMessageHandler();
        var provider = CreateProvider(handler);

        provider.ProviderId.Should().Be("huggingface");
        provider.DisplayName.Should().Be("Hugging Face");
    }

    [Fact]
    public async Task SearchAsync_IncludesAuthToken_WhenAvailable()
    {
        _credentialStore.GetTokenAsync("huggingface", Arg.Any<CancellationToken>())
            .Returns("hf_test_token");

        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, "[]");

        var provider = CreateProvider(handler);
        await provider.SearchAsync(new ModelSearchQuery("huggingface", SearchTerm: "test"));

        handler.SentRequests.Should().ContainSingle();
        handler.SentRequests[0].Headers.Authorization.Should().NotBeNull();
        handler.SentRequests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.SentRequests[0].Headers.Authorization!.Parameter.Should().Be("hf_test_token");
    }

    [Fact]
    public async Task SearchAsync_WithPagination_IncludesOffset()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, "[]");

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("huggingface", SearchTerm: "test", Page: 2, PageSize: 10);
        await provider.SearchAsync(query);

        handler.SentRequests.Should().ContainSingle();
        handler.SentRequests[0].RequestUri!.ToString().Should().Contain("offset=20");
        handler.SentRequests[0].RequestUri!.ToString().Should().Contain("limit=10");
    }

    [Fact]
    public async Task SearchAsync_FullPage_ReportsHasMore()
    {
        // Return exactly PageSize items -> HasMore should be true
        var models = Enumerable.Range(0, 5).Select(i => $$"""{"id": "user/model-{{i}}", "tags": []}""");
        var json = "[" + string.Join(",", models) + "]";

        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, json);

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("huggingface", SearchTerm: "test", PageSize: 5);
        var result = await provider.SearchAsync(query);

        result.HasMore.Should().BeTrue();
        result.Models.Should().HaveCount(5);
    }

    [Fact]
    public async Task SearchAsync_PartialPage_ReportsNoMore()
    {
        var json = """[{"id": "user/model-1", "tags": []}]""";

        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, json);

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("huggingface", SearchTerm: "test", PageSize: 20);
        var result = await provider.SearchAsync(query);

        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_InfersFamilyFromTags()
    {
        var json = """
        [
            {"id": "user/flux-model", "tags": ["flux", "text-to-image"]},
            {"id": "user/sd15-model", "tags": ["stable-diffusion-v1", "text-to-image"]},
            {"id": "user/unknown-model", "tags": ["text-to-image"]}
        ]
        """;

        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, json);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("huggingface", SearchTerm: "test"));

        result.Models[0].Family.Should().Be(ModelFamily.Flux);
        result.Models[1].Family.Should().Be(ModelFamily.SD15);
        result.Models[2].Family.Should().Be(ModelFamily.Unknown);
    }

    [Fact]
    public async Task SearchAsync_ExtractsDescription_WhenPresent()
    {
        var json = """[{"id": "user/model-1", "tags": [], "cardData": {"description": "A great model for art"}}]""";

        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, json);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("huggingface", SearchTerm: "test"));

        result.Models[0].Description.Should().Be("A great model for art");
    }

    [Fact]
    public async Task SearchAsync_TruncatesLongDescription()
    {
        var longDesc = new string('x', 300);
        var json = "[{\"id\": \"user/model-1\", \"tags\": [], \"cardData\": {\"description\": \"" + longDesc + "\"}}]";

        var handler = new MockHttpMessageHandler()
            .WithResponse("huggingface.co/api/models", HttpStatusCode.OK, json);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("huggingface", SearchTerm: "test"));

        result.Models[0].Description!.Length.Should().Be(200);
    }

    [Fact]
    public async Task DownloadAsync_ConstructsCorrectUrl()
    {
        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.OK, "file content");

        var provider = CreateProvider(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), $"hf_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("huggingface", "stabilityai/sdxl-base", "model.safetensors",
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            var result = await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests.Should().ContainSingle();
            handler.SentRequests[0].RequestUri!.ToString().Should()
                .Contain("huggingface.co/stabilityai/sdxl-base/resolve/main/model.safetensors");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WithAuthToken_IncludesBearer()
    {
        _credentialStore.GetTokenAsync("huggingface", Arg.Any<CancellationToken>())
            .Returns("hf_test_token");

        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.OK, "file content");

        var provider = CreateProvider(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), $"hf_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("huggingface", "user/model", "model.safetensors",
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests.Should().ContainSingle();
            handler.SentRequests[0].Headers.Authorization.Should().NotBeNull();
            handler.SentRequests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_HttpError_ReturnsFailure()
    {
        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.NotFound, "Not Found");

        var provider = CreateProvider(handler);
        var request = new DownloadRequest("huggingface", "user/model", null,
            new StorageRoot(Path.GetTempPath(), "Test"), ModelType.Checkpoint);
        var result = await provider.DownloadAsync(request, new Progress<DownloadProgress>());

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DownloadAsync_NoVariantFileName_UsesDefault()
    {
        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.OK, "file content");

        var provider = CreateProvider(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), $"hf_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("huggingface", "user/model", null,
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests[0].RequestUri!.ToString().Should().Contain("model.safetensors");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
