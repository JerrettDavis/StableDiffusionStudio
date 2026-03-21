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

    [Fact]
    public async Task SearchAsync_WithPagination_Uses1BasedPages()
    {
        var emptyResponse = """{"items": [], "metadata": {"totalItems": 0, "currentPage": 2, "totalPages": 5}}""";
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, emptyResponse);

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("civitai", SearchTerm: "test", Page: 1); // 0-based page 1 -> API page 2
        await provider.SearchAsync(query);

        handler.SentRequests.Should().ContainSingle();
        handler.SentRequests[0].RequestUri!.ToString().Should().Contain("page=2");
    }

    [Fact]
    public async Task SearchAsync_IncludesAuthToken_WhenAvailable()
    {
        _credentialStore.GetTokenAsync("civitai", Arg.Any<CancellationToken>())
            .Returns("civit_test_token");

        var emptyResponse = """{"items": [], "metadata": {"totalItems": 0, "currentPage": 1, "totalPages": 0}}""";
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, emptyResponse);

        var provider = CreateProvider(handler);
        await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "test"));

        handler.SentRequests[0].Headers.Authorization.Should().NotBeNull();
        handler.SentRequests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.SentRequests[0].Headers.Authorization!.Parameter.Should().Be("civit_test_token");
    }

    [Fact]
    public async Task SearchAsync_WithFamilyFilter_StillWorks()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, SampleApiResponse);

        var provider = CreateProvider(handler);
        var query = new ModelSearchQuery("civitai", SearchTerm: "test", Family: ModelFamily.SD15);
        var result = await provider.SearchAsync(query);

        // Family filter is applied at the result level by CivitAI, not query param
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_InfersFluxFamily()
    {
        var response = """
        {
            "items": [
                {
                    "id": 99999,
                    "name": "Flux Model",
                    "type": "Checkpoint",
                    "tags": [],
                    "modelVersions": [
                        {
                            "id": 11111,
                            "baseModel": "Flux.1 D",
                            "images": [],
                            "files": [{"id": 22222, "name": "flux.safetensors", "sizeKB": 1024}]
                        }
                    ]
                }
            ],
            "metadata": {"totalItems": 1, "currentPage": 1, "totalPages": 1}
        }
        """;
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, response);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "flux"));

        result.Models[0].Family.Should().Be(ModelFamily.Flux);
    }

    [Fact]
    public async Task DownloadAsync_ConstructsCorrectUrl()
    {
        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.OK, "file content");

        var provider = CreateProvider(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), $"civit_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("civitai", "12345", "model.safetensors",
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            var result = await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests.Should().ContainSingle();
            handler.SentRequests[0].RequestUri!.ToString().Should().Contain("civitai.com/api/v1/download/models/12345");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadAsync_WithApiKey_IncludesTokenInUrl()
    {
        _credentialStore.GetTokenAsync("civitai", Arg.Any<CancellationToken>())
            .Returns("civit_key_123");

        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.OK, "file content");

        var provider = CreateProvider(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), $"civit_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("civitai", "12345", "model.safetensors",
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests[0].RequestUri!.ToString().Should().Contain("token=civit_key_123");
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
        var request = new DownloadRequest("civitai", "12345", null,
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
        var tempDir = Path.Combine(Path.GetTempPath(), $"civit_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("civitai", "12345", null,
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests[0].RequestUri!.ToString().Should().Contain("civitai.com/api/v1/download/models/12345");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task SearchAsync_WithMultipleVersions_UsesFirst()
    {
        var response = """
        {
            "items": [
                {
                    "id": 100,
                    "name": "Multi Version Model",
                    "type": "Checkpoint",
                    "tags": [],
                    "modelVersions": [
                        {
                            "id": 201,
                            "baseModel": "SD 1.5",
                            "images": [{"url": "https://img.com/v2.jpg"}],
                            "files": [{"id": 301, "name": "v2.safetensors", "sizeKB": 2048}]
                        },
                        {
                            "id": 200,
                            "baseModel": "SD 1.5",
                            "images": [{"url": "https://img.com/v1.jpg"}],
                            "files": [{"id": 300, "name": "v1.safetensors", "sizeKB": 1024}]
                        }
                    ]
                }
            ],
            "metadata": {"totalItems": 1, "currentPage": 1, "totalPages": 1}
        }
        """;
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, response);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "test"));

        result.Models[0].PreviewImageUrl.Should().Be("https://img.com/v2.jpg");
        result.Models[0].Variants[0].FileName.Should().Be("v2.safetensors");
    }

    [Fact]
    public async Task SearchAsync_MapsTextualInversionToEmbedding()
    {
        var response = """
        {
            "items": [
                {
                    "id": 777,
                    "name": "EasyNegative",
                    "type": "TextualInversion",
                    "tags": [],
                    "modelVersions": [
                        {
                            "id": 888,
                            "baseModel": "SD 1.5",
                            "images": [],
                            "files": [{"id": 999, "name": "easynegative.safetensors", "sizeKB": 50}]
                        }
                    ]
                }
            ],
            "metadata": {"totalItems": 1, "currentPage": 1, "totalPages": 1}
        }
        """;
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, response);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "easyneg"));

        result.Models[0].Type.Should().Be(ModelType.Embedding);
    }

    [Fact]
    public async Task SearchAsync_MapsVAEType()
    {
        var response = """
        {
            "items": [
                {
                    "id": 555,
                    "name": "SD VAE FT-MSE",
                    "type": "VAE",
                    "tags": [],
                    "modelVersions": [
                        {
                            "id": 666,
                            "baseModel": "SD 1.5",
                            "images": [],
                            "files": [{"id": 777, "name": "vae-ft-mse.safetensors", "sizeKB": 300000}]
                        }
                    ]
                }
            ],
            "metadata": {"totalItems": 1, "currentPage": 1, "totalPages": 1}
        }
        """;
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, response);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "vae"));

        result.Models[0].Type.Should().Be(ModelType.VAE);
    }

    [Fact]
    public async Task SearchAsync_InfersCkptFormatFromFileName()
    {
        var response = """
        {
            "items": [
                {
                    "id": 100,
                    "name": "Old Model",
                    "type": "Checkpoint",
                    "tags": [],
                    "modelVersions": [
                        {
                            "id": 200,
                            "baseModel": "SD 1.5",
                            "images": [],
                            "files": [{"id": 300, "name": "old_model.ckpt", "sizeKB": 2048000}]
                        }
                    ]
                }
            ],
            "metadata": {"totalItems": 1, "currentPage": 1, "totalPages": 1}
        }
        """;
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, response);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "old"));

        result.Models[0].Format.Should().Be(ModelFormat.CKPT);
        result.Models[0].Variants[0].Format.Should().Be(ModelFormat.CKPT);
    }

    [Fact]
    public async Task SearchAsync_HttpException_ReturnsEmptyResult()
    {
        var handler = new MockHttpMessageHandler()
            .WithHandler("civitai.com/api/v1/models", _ => throw new HttpRequestException("Timeout"));

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "test"));

        result.Models.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_ExtractsQuantizationMetadata()
    {
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, SampleApiResponse);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "realistic"));

        result.Models[0].Variants[0].Quantization.Should().Be("fp16");
    }

    [Fact]
    public async Task DownloadAsync_WithoutApiKey_NoTokenInUrl()
    {
        _credentialStore.GetTokenAsync("civitai", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.OK, "file content");

        var provider = CreateProvider(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), $"civit_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("civitai", "12345", "model.safetensors",
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests[0].RequestUri!.ToString().Should().NotContain("token=");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ValidateCredentialsAsync_HttpException_ReturnsFalse()
    {
        _credentialStore.GetTokenAsync("civitai", Arg.Any<CancellationToken>())
            .Returns("some_token");

        var handler = new MockHttpMessageHandler()
            .WithHandler("civitai.com/api/v1/models", _ => throw new HttpRequestException("Network error"));

        var provider = CreateProvider(handler);
        var result = await provider.ValidateCredentialsAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_NoModelVersions_StillReturnsModel()
    {
        var response = """
        {
            "items": [
                {
                    "id": 100,
                    "name": "Empty Model",
                    "type": "Checkpoint",
                    "tags": []
                }
            ],
            "metadata": {"totalItems": 1, "currentPage": 1, "totalPages": 1}
        }
        """;
        var handler = new MockHttpMessageHandler()
            .WithResponse("civitai.com/api/v1/models", HttpStatusCode.OK, response);

        var provider = CreateProvider(handler);
        var result = await provider.SearchAsync(new ModelSearchQuery("civitai", SearchTerm: "empty"));

        result.Models.Should().HaveCount(1);
        result.Models[0].Title.Should().Be("Empty Model");
        result.Models[0].Variants.Should().BeEmpty();
    }

    [Fact]
    public void Capabilities_SupportedModelTypes_IncludesEmbeddingAndControlNet()
    {
        var handler = new MockHttpMessageHandler();
        var provider = CreateProvider(handler);

        provider.Capabilities.SupportedModelTypes.Should().Contain(ModelType.Embedding);
        provider.Capabilities.SupportedModelTypes.Should().Contain(ModelType.ControlNet);
    }

    [Fact]
    public async Task DownloadAsync_NoVariant_UsesDefaultFileName()
    {
        var handler = new MockHttpMessageHandler()
            .WithDefaultResponse(HttpStatusCode.OK, "file content");

        var provider = CreateProvider(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), $"civit_test_{Guid.NewGuid():N}");
        try
        {
            var request = new DownloadRequest("civitai", "99999", null,
                new StorageRoot(tempDir, "Test"), ModelType.Checkpoint);
            await provider.DownloadAsync(request, new Progress<DownloadProgress>());

            handler.SentRequests[0].RequestUri!.ToString().Should().Contain("download/models/99999");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
