using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Tests.Services;

public class ModelCatalogServiceTests
{
    private readonly IModelCatalogRepository _catalogRepo = Substitute.For<IModelCatalogRepository>();
    private readonly IModelProvider _provider = Substitute.For<IModelProvider>();
    private readonly IStorageRootProvider _rootProvider = Substitute.For<IStorageRootProvider>();
    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();
    private readonly ModelCatalogService _service;

    public ModelCatalogServiceTests()
    {
        _provider.ProviderId.Returns("test-provider");
        _provider.DisplayName.Returns("Test Provider");
        _provider.Capabilities.Returns(new ModelProviderCapabilities(
            CanScanLocal: true, CanSearch: true, CanDownload: false,
            RequiresAuth: false, SupportedModelTypes: Enum.GetValues<ModelType>().ToList()));
        _service = new ModelCatalogService(_catalogRepo, new[] { _provider }, _rootProvider, _jobQueue);
    }

    [Fact]
    public async Task ScanAsync_ScansAllRoots_UpsertDiscoveredModels()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync().Returns(new[] { root });
        var discovered = new DiscoveredModel("/models/m.safetensors", "Found Model", ModelType.Checkpoint,
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, null, null, Array.Empty<string>());
        _provider.ScanLocalAsync(root).Returns(new[] { discovered });
        _catalogRepo.GetByFilePathAsync("/models/m.safetensors").Returns((ModelRecord?)null);

        var result = await _service.ScanAsync(new ScanModelsCommand(null));

        result.NewCount.Should().Be(1);
        await _catalogRepo.Received(1).UpsertAsync(Arg.Any<ModelRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_WithSpecificRoot_ScansOnlyThatRoot()
    {
        var root = new StorageRoot("/specific", "Specific");
        _rootProvider.GetRootsAsync().Returns(new[] { root, new StorageRoot("/other", "Other") });
        _provider.ScanLocalAsync(Arg.Is<StorageRoot>(r => r.Path == "/specific")).Returns(Array.Empty<DiscoveredModel>());

        await _service.ScanAsync(new ScanModelsCommand("/specific"));

        await _provider.Received(1).ScanLocalAsync(Arg.Is<StorageRoot>(r => r.Path == "/specific"), Arg.Any<CancellationToken>());
        await _provider.DidNotReceive().ScanLocalAsync(Arg.Is<StorageRoot>(r => r.Path == "/other"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_ExistingModel_UpdatesInsteadOfCreating()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync().Returns(new[] { root });
        var existing = ModelRecord.Create("Existing", "/models/m.safetensors",
            ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "test-provider");
        _catalogRepo.GetByFilePathAsync("/models/m.safetensors").Returns(existing);
        var discovered = new DiscoveredModel("/models/m.safetensors", "Scanned", ModelType.Checkpoint,
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, null, null, Array.Empty<string>());
        _provider.ScanLocalAsync(root).Returns(new[] { discovered });

        var result = await _service.ScanAsync(new ScanModelsCommand(null));

        result.UpdatedCount.Should().Be(1);
        result.NewCount.Should().Be(0);
    }

    [Fact]
    public async Task ListAsync_DelegatesToRepository()
    {
        var filter = new ModelFilter();
        var records = new List<ModelRecord>
        {
            ModelRecord.Create("A", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local")
        };
        _catalogRepo.ListAsync(filter).Returns(records);

        var result = await _service.ListAsync(filter);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("A");
    }

    [Fact]
    public async Task ScanAsync_SkipsProviderWithoutLocalScanCapability()
    {
        var nonScanProvider = Substitute.For<IModelProvider>();
        nonScanProvider.ProviderId.Returns("remote-only");
        nonScanProvider.Capabilities.Returns(new ModelProviderCapabilities(
            CanScanLocal: false, CanSearch: true, CanDownload: true,
            RequiresAuth: false, SupportedModelTypes: Enum.GetValues<ModelType>().ToList()));

        var service = new ModelCatalogService(_catalogRepo, new[] { nonScanProvider }, _rootProvider, _jobQueue);
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync().Returns(new[] { root });

        await service.ScanAsync(new ScanModelsCommand(null));

        await nonScanProvider.DidNotReceive().ScanLocalAsync(Arg.Any<StorageRoot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_DispatchesToCorrectProvider()
    {
        var expectedResult = new SearchResult(new List<RemoteModelInfo>
        {
            new("ext-1", "Model A", null, ModelType.Checkpoint, ModelFamily.SD15, ModelFormat.SafeTensors,
                2_000_000_000L, null, Array.Empty<string>(), "https://example.com", Array.Empty<ModelFileVariant>())
        }, 1, false);

        _provider.SearchAsync(Arg.Any<ModelSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var query = new ModelSearchQuery("test-provider", SearchTerm: "Model A");
        var result = await _service.SearchAsync(query);

        result.Models.Should().HaveCount(1);
        result.Models[0].Title.Should().Be("Model A");
        await _provider.Received(1).SearchAsync(Arg.Is<ModelSearchQuery>(q => q.ProviderId == "test-provider"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WithUnknownProvider_ReturnsEmptyResult()
    {
        var query = new ModelSearchQuery("unknown-provider", SearchTerm: "anything");
        var result = await _service.SearchAsync(query);

        result.Models.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task RequestDownloadAsync_EnqueuesJobWithSerializedRequest()
    {
        var expectedJobId = Guid.NewGuid();
        _jobQueue.EnqueueAsync("model-download", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedJobId);

        var request = new DownloadRequest("test-provider", "ext-123", null,
            new StorageRoot("/models", "Models"), ModelType.Checkpoint);
        var jobId = await _service.RequestDownloadAsync(request);

        jobId.Should().Be(expectedJobId);
        await _jobQueue.Received(1).EnqueueAsync("model-download", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetProviders_ReturnsProviderInfoList()
    {
        var providers = _service.GetProviders();

        providers.Should().HaveCount(1);
        providers[0].ProviderId.Should().Be("test-provider");
        providers[0].DisplayName.Should().Be("Test Provider");
        providers[0].Capabilities.CanScanLocal.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsDto()
    {
        var record = ModelRecord.Create("Test Model", "/path/model.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "local");
        _catalogRepo.GetByIdAsync(record.Id, Arg.Any<CancellationToken>()).Returns(record);

        var result = await _service.GetByIdAsync(record.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Test Model");
        result.ModelFamily.Should().Be(ModelFamily.SD15);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        _catalogRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ModelRecord?)null);

        var result = await _service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_WithNonSearchableProvider_ReturnsEmpty()
    {
        var nonSearchProvider = Substitute.For<IModelProvider>();
        nonSearchProvider.ProviderId.Returns("no-search");
        nonSearchProvider.Capabilities.Returns(new ModelProviderCapabilities(
            CanScanLocal: true, CanSearch: false, CanDownload: false,
            RequiresAuth: false, SupportedModelTypes: Enum.GetValues<ModelType>().ToList()));

        var service = new ModelCatalogService(_catalogRepo, new[] { nonSearchProvider }, _rootProvider, _jobQueue);
        var query = new ModelSearchQuery("no-search", SearchTerm: "anything");

        var result = await service.SearchAsync(query);

        result.Models.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ScanAsync_WithNoRoots_ReturnsZeroCounts()
    {
        _rootProvider.GetRootsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<StorageRoot>());

        var result = await _service.ScanAsync(new ScanModelsCommand(null));

        result.NewCount.Should().Be(0);
        result.UpdatedCount.Should().Be(0);
    }
}
