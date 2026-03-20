using FluentAssertions;
using Microsoft.Data.Sqlite;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Application.Tests.Integration;

public class ModelCatalogServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ModelCatalogService _service;
    private readonly IModelProvider _localProvider;
    private readonly IModelProvider _remoteProvider;
    private readonly IStorageRootProvider _rootProvider;
    private readonly IJobQueue _jobQueue;

    public ModelCatalogServiceIntegrationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var repo = new ModelCatalogRepository(_context);

        _localProvider = Substitute.For<IModelProvider>();
        _localProvider.ProviderId.Returns("local-folder");
        _localProvider.DisplayName.Returns("Local Folder");
        _localProvider.Capabilities.Returns(new ModelProviderCapabilities(
            CanScanLocal: true, CanSearch: false, CanDownload: false,
            RequiresAuth: false, SupportedModelTypes: [ModelType.Checkpoint]));

        _remoteProvider = Substitute.For<IModelProvider>();
        _remoteProvider.ProviderId.Returns("huggingface");
        _remoteProvider.DisplayName.Returns("Hugging Face");
        _remoteProvider.Capabilities.Returns(new ModelProviderCapabilities(
            CanScanLocal: false, CanSearch: true, CanDownload: true,
            RequiresAuth: false, SupportedModelTypes: [ModelType.Checkpoint]));

        _rootProvider = Substitute.For<IStorageRootProvider>();
        _jobQueue = Substitute.For<IJobQueue>();
        _jobQueue.EnqueueAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        _service = new ModelCatalogService(repo, [_localProvider, _remoteProvider], _rootProvider, _jobQueue);
    }

    [Fact]
    public async Task ScanAsync_PersistsDiscoveredModels()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StorageRoot> { root });

        _localProvider.ScanLocalAsync(root, Arg.Any<CancellationToken>())
            .Returns(new List<DiscoveredModel>
            {
                new("/models/sd15.safetensors", "SD 1.5 Model", ModelType.Checkpoint,
                    ModelFamily.SD15, ModelFormat.SafeTensors, 2048000, null, null, Array.Empty<string>()),
                new("/models/sdxl.safetensors", "SDXL Base", ModelType.Checkpoint,
                    ModelFamily.SDXL, ModelFormat.SafeTensors, 4096000, "/models/sdxl.preview.png",
                    "An SDXL model", new[] { "sdxl", "base" })
            });

        var result = await _service.ScanAsync(new ScanModelsCommand(null));

        result.NewCount.Should().Be(2);
        result.UpdatedCount.Should().Be(0);

        var models = await _service.ListAsync(new ModelFilter());
        models.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScanAsync_UpdatesExistingModels()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StorageRoot> { root });

        var discovered = new List<DiscoveredModel>
        {
            new("/models/sd15.safetensors", "SD 1.5", ModelType.Checkpoint,
                ModelFamily.SD15, ModelFormat.SafeTensors, 2048000, null, null, Array.Empty<string>())
        };
        _localProvider.ScanLocalAsync(root, Arg.Any<CancellationToken>())
            .Returns(discovered);

        // First scan
        await _service.ScanAsync(new ScanModelsCommand(null));

        // Second scan with same file
        var result = await _service.ScanAsync(new ScanModelsCommand(null));

        result.NewCount.Should().Be(0);
        result.UpdatedCount.Should().Be(1);

        var models = await _service.ListAsync(new ModelFilter());
        models.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_WithDescription_PersistsDescription()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StorageRoot> { root });

        _localProvider.ScanLocalAsync(root, Arg.Any<CancellationToken>())
            .Returns(new List<DiscoveredModel>
            {
                new("/models/test.safetensors", "Test Model", ModelType.Checkpoint,
                    ModelFamily.SD15, ModelFormat.SafeTensors, 1024, null,
                    "A test description", new[] { "test", "model" })
            });

        await _service.ScanAsync(new ScanModelsCommand(null));

        var models = await _service.ListAsync(new ModelFilter());
        models.Should().HaveCount(1);
        models[0].Description.Should().Be("A test description");
        models[0].Tags.Should().Contain("test");
    }

    [Fact]
    public async Task ListAsync_ReturnsPersistedModels()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StorageRoot> { root });

        _localProvider.ScanLocalAsync(root, Arg.Any<CancellationToken>())
            .Returns(new List<DiscoveredModel>
            {
                new("/models/a.safetensors", "Model A", ModelType.Checkpoint,
                    ModelFamily.SD15, ModelFormat.SafeTensors, 1024, null, null, Array.Empty<string>()),
                new("/models/b.ckpt", "Model B", ModelType.Checkpoint,
                    ModelFamily.SDXL, ModelFormat.CKPT, 2048, null, null, Array.Empty<string>())
            });

        await _service.ScanAsync(new ScanModelsCommand(null));

        var allModels = await _service.ListAsync(new ModelFilter());
        allModels.Should().HaveCount(2);

        var sd15Only = await _service.ListAsync(new ModelFilter(Family: ModelFamily.SD15));
        sd15Only.Should().HaveCount(1);
        sd15Only[0].Title.Should().Be("Model A");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectDto()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<StorageRoot> { root });

        _localProvider.ScanLocalAsync(root, Arg.Any<CancellationToken>())
            .Returns(new List<DiscoveredModel>
            {
                new("/models/specific.safetensors", "Specific Model", ModelType.Checkpoint,
                    ModelFamily.SD15, ModelFormat.SafeTensors, 4096, null, null, Array.Empty<string>())
            });

        await _service.ScanAsync(new ScanModelsCommand(null));

        var models = await _service.ListAsync(new ModelFilter());
        var id = models[0].Id;

        var dto = await _service.GetByIdAsync(id);

        dto.Should().NotBeNull();
        dto!.Title.Should().Be("Specific Model");
        dto.FilePath.Should().Be("/models/specific.safetensors");
        dto.Format.Should().Be(ModelFormat.SafeTensors);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var dto = await _service.GetByIdAsync(Guid.NewGuid());
        dto.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_DelegatesToProvider()
    {
        var expectedResult = new SearchResult(
            new List<RemoteModelInfo>
            {
                new("ext-1", "Remote Model", "A model", ModelType.Checkpoint,
                    ModelFamily.SD15, ModelFormat.SafeTensors, 2048000, null,
                    Array.Empty<string>(), "https://example.com/model", Array.Empty<ModelFileVariant>())
            }, 1, false);

        _remoteProvider.SearchAsync(Arg.Any<ModelSearchQuery>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var query = new ModelSearchQuery("huggingface", SearchTerm: "test");
        var result = await _service.SearchAsync(query);

        result.Models.Should().HaveCount(1);
        result.Models[0].Title.Should().Be("Remote Model");
    }

    [Fact]
    public async Task SearchAsync_UnknownProvider_ReturnsEmpty()
    {
        var query = new ModelSearchQuery("nonexistent-provider", SearchTerm: "test");
        var result = await _service.SearchAsync(query);

        result.Models.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task RequestDownloadAsync_EnqueuesJob()
    {
        var request = new DownloadRequest("huggingface", "ext-1", "model.safetensors",
            new StorageRoot("/models", "Models"), ModelType.Checkpoint);

        var jobId = await _service.RequestDownloadAsync(request);

        jobId.Should().NotBeEmpty();
        await _jobQueue.Received(1).EnqueueAsync("model-download", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GetProviders_ReturnsBothProviders()
    {
        var providers = _service.GetProviders();

        providers.Should().HaveCount(2);
        providers.Should().Contain(p => p.ProviderId == "local-folder");
        providers.Should().Contain(p => p.ProviderId == "huggingface");
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
