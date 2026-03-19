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
}
