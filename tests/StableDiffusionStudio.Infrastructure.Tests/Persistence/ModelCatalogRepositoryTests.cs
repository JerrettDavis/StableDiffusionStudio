using FluentAssertions;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public class ModelCatalogRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly ModelCatalogRepository _repo;

    public ModelCatalogRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new ModelCatalogRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task UpsertAsync_NewRecord_AddsToDatabase()
    {
        var record = ModelRecord.Create("Test Model", "/path/model.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "local");

        await _repo.UpsertAsync(record);

        var retrieved = await _repo.GetByIdAsync(record.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Test Model");
    }

    [Fact]
    public async Task GetByFilePathAsync_ExistingPath_ReturnsRecord()
    {
        var record = ModelRecord.Create(null, "/models/test.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local");
        await _repo.UpsertAsync(record);

        var found = await _repo.GetByFilePathAsync("/models/test.safetensors");

        found.Should().NotBeNull();
        found!.Id.Should().Be(record.Id);
    }

    [Fact]
    public async Task ListAsync_WithFamilyFilter_FiltersCorrectly()
    {
        await _repo.UpsertAsync(ModelRecord.Create("SD15", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local"));
        await _repo.UpsertAsync(ModelRecord.Create("SDXL", "/b.safetensors", ModelFamily.SDXL, ModelFormat.SafeTensors, 1000, "local"));

        var results = await _repo.ListAsync(new ModelFilter(Family: ModelFamily.SD15));

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("SD15");
    }

    [Fact]
    public async Task ListAsync_WithSearchTerm_SearchesTitle()
    {
        await _repo.UpsertAsync(ModelRecord.Create("Dreamshaper", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local"));
        await _repo.UpsertAsync(ModelRecord.Create("Realistic", "/b.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local"));

        var results = await _repo.ListAsync(new ModelFilter(SearchTerm: "Dream"));

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("Dreamshaper");
    }

    [Fact]
    public async Task RemoveAsync_DeletesRecord()
    {
        var record = ModelRecord.Create(null, "/path/m.safetensors", ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "local");
        await _repo.UpsertAsync(record);

        await _repo.RemoveAsync(record.Id);

        var retrieved = await _repo.GetByIdAsync(record.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WithModelTypeFilter_FiltersCorrectly()
    {
        await _repo.UpsertAsync(ModelRecord.Create("Checkpoint", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local", ModelType.Checkpoint));
        await _repo.UpsertAsync(ModelRecord.Create("LoRA", "/b.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 100, "local", ModelType.LoRA));

        var results = await _repo.ListAsync(new ModelFilter(Type: ModelType.LoRA));

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("LoRA");
    }

    [Fact]
    public async Task ListAsync_WithMultipleFiltersCombined()
    {
        await _repo.UpsertAsync(ModelRecord.Create("SD15 Checkpoint", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local", ModelType.Checkpoint));
        await _repo.UpsertAsync(ModelRecord.Create("SDXL Checkpoint", "/b.safetensors", ModelFamily.SDXL, ModelFormat.SafeTensors, 1000, "local", ModelType.Checkpoint));
        await _repo.UpsertAsync(ModelRecord.Create("SD15 LoRA", "/c.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 100, "local", ModelType.LoRA));

        var results = await _repo.ListAsync(new ModelFilter(Family: ModelFamily.SD15, Type: ModelType.Checkpoint));

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("SD15 Checkpoint");
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingRecord()
    {
        var record = ModelRecord.Create("Original", "/path/model.safetensors",
            ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "local");
        await _repo.UpsertAsync(record);

        record.UpdateMetadata(title: "Updated Title", modelFamily: ModelFamily.SDXL);
        await _repo.UpsertAsync(record);

        _context.ChangeTracker.Clear();
        var retrieved = await _repo.GetByIdAsync(record.Id);
        retrieved!.Title.Should().Be("Updated Title");
        retrieved.ModelFamily.Should().Be(ModelFamily.SDXL);
    }

    [Fact]
    public async Task GetByFilePathAsync_NonExistentPath_ReturnsNull()
    {
        var result = await _repo.GetByFilePathAsync("/nonexistent/path.safetensors");
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_NoFilter_ReturnsAll()
    {
        await _repo.UpsertAsync(ModelRecord.Create("A", "/a.safetensors", ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local"));
        await _repo.UpsertAsync(ModelRecord.Create("B", "/b.safetensors", ModelFamily.SDXL, ModelFormat.SafeTensors, 1000, "local"));
        await _repo.UpsertAsync(ModelRecord.Create("C", "/c.safetensors", ModelFamily.Flux, ModelFormat.SafeTensors, 1000, "local"));

        var results = await _repo.ListAsync(new ModelFilter());

        results.Should().HaveCount(3);
    }
}
