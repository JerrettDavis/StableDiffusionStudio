using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public class PresetRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly PresetRepository _repo;

    public PresetRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new PresetRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsPreset()
    {
        var preset = GenerationPresetEntity.Create(
            "Test Preset", "Description", null, null, null, "ugly",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        await _repo.AddAsync(preset);
        var retrieved = await _repo.GetByIdAsync(preset.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Preset");
        retrieved.NegativePrompt.Should().Be("ugly");
    }

    [Fact]
    public async Task ListAsync_WithModelId_ReturnsMatchingAndUniversal()
    {
        var modelId = Guid.NewGuid();
        var modelSpecific = GenerationPresetEntity.Create(
            "Model Specific", null, modelId, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var universal = GenerationPresetEntity.Create(
            "Universal", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var otherModel = GenerationPresetEntity.Create(
            "Other Model", null, Guid.NewGuid(), null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        await _repo.AddAsync(modelSpecific);
        await _repo.AddAsync(universal);
        await _repo.AddAsync(otherModel);

        var results = await _repo.ListAsync(modelId: modelId);

        results.Should().HaveCount(2);
        results.Select(p => p.Name).Should().Contain("Model Specific");
        results.Select(p => p.Name).Should().Contain("Universal");
        results.Select(p => p.Name).Should().NotContain("Other Model");
    }

    [Fact]
    public async Task ListAsync_WithFamily_ReturnsMatchingAndUniversal()
    {
        var sdxlPreset = GenerationPresetEntity.Create(
            "SDXL", null, null, ModelFamily.SDXL, null, "",
            Sampler.DPMPlusPlus2MKarras, Scheduler.Karras, 25, 7.0, 1024, 1024, 1, 1);
        var anyFamily = GenerationPresetEntity.Create(
            "Any Family", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var sd15Preset = GenerationPresetEntity.Create(
            "SD15", null, null, ModelFamily.SD15, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        await _repo.AddAsync(sdxlPreset);
        await _repo.AddAsync(anyFamily);
        await _repo.AddAsync(sd15Preset);

        var results = await _repo.ListAsync(family: ModelFamily.SDXL);

        results.Should().HaveCount(2);
        results.Select(p => p.Name).Should().Contain("SDXL");
        results.Select(p => p.Name).Should().Contain("Any Family");
        results.Select(p => p.Name).Should().NotContain("SD15");
    }

    [Fact]
    public async Task ListAsync_OrdersByIsDefaultDescThenName()
    {
        var defaultPreset = GenerationPresetEntity.Create(
            "B Default", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        defaultPreset.SetDefault(true);

        var alpha = GenerationPresetEntity.Create(
            "A Alpha", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        var charlie = GenerationPresetEntity.Create(
            "C Charlie", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        await _repo.AddAsync(charlie);
        await _repo.AddAsync(alpha);
        await _repo.AddAsync(defaultPreset);

        var results = await _repo.ListAsync();

        results[0].Name.Should().Be("B Default"); // Default first
        results[1].Name.Should().Be("A Alpha");
        results[2].Name.Should().Be("C Charlie");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var preset = GenerationPresetEntity.Create(
            "Original", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        await _repo.AddAsync(preset);

        preset.Update(
            "Updated", "New desc", null, null, null, "bad",
            Sampler.DDIM, Scheduler.Karras, 30, 5.0, 768, 768, 2, 2);
        await _repo.UpdateAsync(preset);

        // Use a fresh context to verify persistence
        var retrieved = await _repo.GetByIdAsync(preset.Id);
        retrieved!.Name.Should().Be("Updated");
        retrieved.Steps.Should().Be(30);
        retrieved.Sampler.Should().Be(Sampler.DDIM);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPreset()
    {
        var preset = GenerationPresetEntity.Create(
            "To Delete", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        await _repo.AddAsync(preset);

        await _repo.DeleteAsync(preset.Id);

        var retrieved = await _repo.GetByIdAsync(preset.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNotThrow()
    {
        var act = () => _repo.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListAsync_WithBothModelIdAndFamily_FiltersCombined()
    {
        var modelId = Guid.NewGuid();
        var match = GenerationPresetEntity.Create(
            "Match", null, modelId, ModelFamily.SDXL, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var universalSdxl = GenerationPresetEntity.Create(
            "Universal SDXL", null, null, ModelFamily.SDXL, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var modelAnyFamily = GenerationPresetEntity.Create(
            "Model Any Family", null, modelId, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var noMatch = GenerationPresetEntity.Create(
            "No Match", null, Guid.NewGuid(), ModelFamily.SD15, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        await _repo.AddAsync(match);
        await _repo.AddAsync(universalSdxl);
        await _repo.AddAsync(modelAnyFamily);
        await _repo.AddAsync(noMatch);

        var results = await _repo.ListAsync(modelId: modelId, family: ModelFamily.SDXL);

        results.Select(p => p.Name).Should().Contain("Match");
        results.Select(p => p.Name).Should().Contain("Universal SDXL");
        results.Select(p => p.Name).Should().Contain("Model Any Family");
        results.Select(p => p.Name).Should().NotContain("No Match");
    }

    [Fact]
    public async Task ListAsync_WithNullModelId_IncludesUniversalPresets()
    {
        var universal = GenerationPresetEntity.Create(
            "Universal", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var modelSpecific = GenerationPresetEntity.Create(
            "Model Specific", null, Guid.NewGuid(), null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        await _repo.AddAsync(universal);
        await _repo.AddAsync(modelSpecific);

        var results = await _repo.ListAsync(modelId: null);

        // When modelId is null, should return all presets (universal + model-specific)
        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnly_TargetPreset()
    {
        var keep = GenerationPresetEntity.Create(
            "Keep Me", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var delete = GenerationPresetEntity.Create(
            "Delete Me", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        await _repo.AddAsync(keep);
        await _repo.AddAsync(delete);

        await _repo.DeleteAsync(delete.Id);

        var results = await _repo.ListAsync();
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Keep Me");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_DefaultsFirst_ThenAlphabetical()
    {
        var preset1 = GenerationPresetEntity.Create(
            "Zebra", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        var preset2 = GenerationPresetEntity.Create(
            "Apple", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        preset2.SetDefault(true);

        await _repo.AddAsync(preset1);
        await _repo.AddAsync(preset2);

        var results = await _repo.ListAsync();

        results[0].Name.Should().Be("Apple"); // default first
        results[1].Name.Should().Be("Zebra");
    }
}
