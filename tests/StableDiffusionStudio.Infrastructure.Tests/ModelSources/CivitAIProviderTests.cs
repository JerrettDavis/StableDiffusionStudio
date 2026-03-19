using System.Net;
using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.ModelSources;

namespace StableDiffusionStudio.Infrastructure.Tests.ModelSources;

public class CivitAIProviderTests
{
    private readonly IProviderCredentialStore _credentialStore = Substitute.For<IProviderCredentialStore>();

    private CivitAIProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var downloadClient = new HttpDownloadClient(httpClient);
        return new CivitAIProvider(httpClient, downloadClient, _credentialStore);
    }

    private const string SampleApiResponse = """
    {
        "items": [
            {
                "id": 12345,
                "name": "Realistic Vision V5.1",
                "description": "<p>A photorealistic model</p>",
                "type": "Checkpoint",
                "tags": ["photorealistic", "realistic"],
                "modelVersions": [
                    {
                        "id": 67890,
                        "baseModel": "SD 1.5",
                        "images": [
                            { "url": "https://image.civitai.com/preview.jpg" }
                        ],
                        "files": [
                            {
                                "id": 99999,
                                "name": "realisticVision_v51.safetensors",
                                "sizeKB": 2048000,
                                "metadata": { "fp": "fp16" }
                            }
                        ]
                    }
                ]
            },
            {
                "id": 54321,
                "name": "Detail Tweaker LoRA",
                "description": "Enhances details",
                "type": "LORA",
                "tags": ["detail", "enhancement"],
                "modelVersions": [
                    {
                        "id": 11111,
                        "baseModel": "SDXL 1.0",
                        "images": [],
                        "files": [
                            {
                                "id": 22222,
                                "name": "detailTweaker.safetensors",
                                "sizeKB": 144000,
                                "metadata": { "fp": "fp16" }
                            }
                        ]
                    }
                ]
            }
        ],
        "metadata": {
            "totalItems": 100,
            "currentPage": 1,
            "totalPages": 5
        }
    }
    """;

    [Fact]
    public async Task SearchAsync_ReturnsModelsFromApi()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, SampleApiResponse);

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("civitai", SearchTerm: "realistic");

        var result = await provider.SearchAsync(query);

        result.Models.Should().HaveCount(2);
        result.TotalCount.Should().Be(100);
        result.HasMore.Should().BeTrue();

        result.Models[0].Title.Should().Be("Realistic Vision V5.1");
        result.Models[0].Type.Should().Be(ModelType.Checkpoint);
        result.Models[0].Family.Should().Be(ModelFamily.SD15);
        result.Models[0].PreviewImageUrl.Should().Be("https://image.civitai.com/preview.jpg");
        result.Models[0].Description.Should().Be("A photorealistic model");
        result.Models[0].Variants.Should().HaveCount(1);
        result.Models[0].Variants[0].FileName.Should().Be("realisticVision_v51.safetensors");
        result.Models[0].Variants[0].Format.Should().Be(ModelFormat.SafeTensors);

        result.Models[1].Title.Should().Be("Detail Tweaker LoRA");
        result.Models[1].Type.Should().Be(ModelType.LoRA);
        result.Models[1].Family.Should().Be(ModelFamily.SDXL);
    }

    [Fact]
    public async Task SearchAsync_EmptyResult_ReturnsEmpty()
    {
        var emptyResponse = """{"items": [], "metadata": {"totalItems": 0, "currentPage": 1, "totalPages": 0}}""";
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, emptyResponse);

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("civitai", SearchTerm: "nonexistent");

        var result = await provider.SearchAsync(query);

        result.Models.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_ApiError_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.InternalServerError, "error");

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("civitai", SearchTerm: "test");

        var result = await provider.SearchAsync(query);

        result.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithTypeFilter_IncludesTypeInQuery()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK,
                """{"items": [], "metadata": {"totalItems": 0, "currentPage": 1, "totalPages": 0}}""");

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("civitai", SearchTerm: "test", Type: ModelType.LoRA);
        await provider.SearchAsync(query);

        handler.SentRequests.Should().ContainSingle();
        handler.SentRequests[0].RequestUri!.ToString().Should().Contain("types=LORA");
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
    }

    [Fact]
    public async Task ValidateCredentialsAsync_NoToken_ReturnsTrue()
    {
        _credentialStore.GetTokenAsync("civitai", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var handler = new MockHttpMessageHandler();
        var provider = CreateProvider(handler);

        var result = await provider.ValidateCredentialsAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ValidToken_ReturnsTrue()
    {
        _credentialStore.GetTokenAsync("civitai", Arg.Any<CancellationToken>())
            .Returns("civit_valid_key");

        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models?limit=1", HttpStatusCode.OK,
                """{"items": [], "metadata": {"totalItems": 0}}""");

        var provider = CreateProvider(handler);
        var result = await provider.ValidateCredentialsAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_InvalidToken_ReturnsFalse()
    {
        _credentialStore.GetTokenAsync("civitai", Arg.Any<CancellationToken>())
            .Returns("civit_invalid_key");

        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.Unauthorized, "Unauthorized");

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
    public void ProviderId_IsCivitai()
    {
        var handler = new MockHttpMessageHandler();
        var provider = CreateProvider(handler);

        provider.ProviderId.Should().Be("civitai");
        provider.DisplayName.Should().Be("CivitAI");
    }

    [Fact]
    public async Task SearchAsync_StripHtmlFromDescription()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, SampleApiResponse);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "realistic"));

        // HTML tags should be stripped from description
        result.Models[0].Description.Should().NotContain("<p>");
        result.Models[0].Description.Should().NotContain("</p>");
        result.Models[0].Description.Should().Be("A photorealistic model");
    }
}
