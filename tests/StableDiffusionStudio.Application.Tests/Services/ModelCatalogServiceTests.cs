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
    private readonly IModelSourceAdapter _adapter = Substitute.For<IModelSourceAdapter>();
    private readonly IStorageRootProvider _rootProvider = Substitute.For<IStorageRootProvider>();
    private readonly ModelCatalogService _service;

    public ModelCatalogServiceTests()
    {
        _adapter.SourceName.Returns("test-adapter");
        _service = new ModelCatalogService(_catalogRepo, new[] { _adapter }, _rootProvider);
    }

    [Fact]
    public async Task ScanAsync_ScansAllRoots_UpsertDiscoveredModels()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync().Returns(new[] { root });
        var scannedModel = ModelRecord.Create("Found Model", "/models/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "test-adapter");
        _adapter.ScanAsync(root).Returns(new[] { scannedModel });
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
        _adapter.ScanAsync(Arg.Is<StorageRoot>(r => r.Path == "/specific")).Returns(Array.Empty<ModelRecord>());

        await _service.ScanAsync(new ScanModelsCommand("/specific"));

        await _adapter.Received(1).ScanAsync(Arg.Is<StorageRoot>(r => r.Path == "/specific"), Arg.Any<CancellationToken>());
        await _adapter.DidNotReceive().ScanAsync(Arg.Is<StorageRoot>(r => r.Path == "/other"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScanAsync_ExistingModel_UpdatesInsteadOfCreating()
    {
        var root = new StorageRoot("/models", "Models");
        _rootProvider.GetRootsAsync().Returns(new[] { root });
        var existing = ModelRecord.Create("Existing", "/models/m.safetensors",
            ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "test-adapter");
        _catalogRepo.GetByFilePathAsync("/models/m.safetensors").Returns(existing);
        var scanned = ModelRecord.Create("Scanned", "/models/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "test-adapter");
        _adapter.ScanAsync(root).Returns(new[] { scanned });

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
}
