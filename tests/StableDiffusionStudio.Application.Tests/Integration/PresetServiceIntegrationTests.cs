using FluentAssertions;
using Microsoft.Data.Sqlite;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Application.Tests.Integration;

public class PresetServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly PresetService _service;

    public PresetServiceIntegrationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var repo = new PresetRepository(_context);
        _service = new PresetService(repo);
    }

    private SavePresetCommand CreateCommand(
        string name = "Default Preset",
        Guid? id = null,
        Guid? modelId = null,
        ModelFamily? family = null,
        bool isDefault = false) => new(
        Id: id,
        Name: name,
        Description: "A test preset",
        AssociatedModelId: modelId,
        ModelFamilyFilter: family,
        IsDefault: isDefault,
        PositivePromptTemplate: "masterpiece, best quality",
        NegativePrompt: "ugly, blurry",
        Sampler: Sampler.EulerA,
        Scheduler: Scheduler.Normal,
        Steps: 20,
        CfgScale: 7.0,
        Width: 512,
        Height: 512,
        BatchSize: 1,
        ClipSkip: 1);

    [Fact]
    public async Task SaveAndList_RoundTrips()
    {
        var dto = await _service.SavePresetAsync(CreateCommand("My Preset"));

        dto.Id.Should().NotBeEmpty();
        dto.Name.Should().Be("My Preset");
        dto.Steps.Should().Be(20);

        var list = await _service.ListPresetsAsync();
        list.Should().HaveCount(1);
        list[0].Name.Should().Be("My Preset");
    }

    [Fact]
    public async Task SaveExisting_Updates()
    {
        var created = await _service.SavePresetAsync(CreateCommand("Original"));

        var updated = await _service.SavePresetAsync(CreateCommand("Updated", id: created.Id));

        updated.Id.Should().Be(created.Id);
        updated.Name.Should().Be("Updated");

        var list = await _service.ListPresetsAsync();
        list.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListAsync_FiltersByModelId()
    {
        var modelA = Guid.NewGuid();
        var modelB = Guid.NewGuid();

        await _service.SavePresetAsync(CreateCommand("For Model A", modelId: modelA));
        await _service.SavePresetAsync(CreateCommand("For Model B", modelId: modelB));
        await _service.SavePresetAsync(CreateCommand("Universal")); // no model ID

        var list = await _service.ListPresetsAsync(modelId: modelA);

        // Should include Model A specific + universal (null model ID)
        list.Should().HaveCount(2);
        list.Should().Contain(p => p.Name == "For Model A");
        list.Should().Contain(p => p.Name == "Universal");
    }

    [Fact]
    public async Task ListAsync_FiltersByFamily()
    {
        await _service.SavePresetAsync(CreateCommand("SD15 Preset", family: ModelFamily.SD15));
        await _service.SavePresetAsync(CreateCommand("SDXL Preset", family: ModelFamily.SDXL));
        await _service.SavePresetAsync(CreateCommand("Universal Preset")); // no family

        var list = await _service.ListPresetsAsync(family: ModelFamily.SD15);

        // Should include SD15 specific + universal (null family)
        list.Should().HaveCount(2);
        list.Should().Contain(p => p.Name == "SD15 Preset");
        list.Should().Contain(p => p.Name == "Universal Preset");
    }

    [Fact]
    public async Task ListAsync_IncludesUniversalPresets()
    {
        await _service.SavePresetAsync(CreateCommand("Universal"));
        await _service.SavePresetAsync(CreateCommand("Model-Specific", modelId: Guid.NewGuid()));

        var list = await _service.ListPresetsAsync(modelId: Guid.NewGuid()); // Different model ID

        // Should include only the universal preset (null model ID matches any)
        list.Should().HaveCount(1);
        list[0].Name.Should().Be("Universal");
    }

    [Fact]
    public async Task DeleteAsync_Removes()
    {
        var created = await _service.SavePresetAsync(CreateCommand("Deletable"));

        await _service.DeletePresetAsync(created.Id);

        var list = await _service.ListPresetsAsync();
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPresetAsync_ReturnsCorrectDto()
    {
        var created = await _service.SavePresetAsync(CreateCommand("Specific"));

        var dto = await _service.GetPresetAsync(created.Id);

        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Specific");
        dto.Sampler.Should().Be(Sampler.EulerA);
        dto.NegativePrompt.Should().Be("ugly, blurry");
    }

    [Fact]
    public async Task GetPresetAsync_NonExistent_ReturnsNull()
    {
        var dto = await _service.GetPresetAsync(Guid.NewGuid());
        dto.Should().BeNull();
    }

    [Fact]
    public async Task ApplyPresetToParameters_ReturnsDto()
    {
        var created = await _service.SavePresetAsync(CreateCommand("Apply Me"));

        var dto = await _service.ApplyPresetToParameters(created.Id);

        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Apply Me");
    }

    [Fact]
    public async Task SavePresetAsync_WithIsDefault_PersistsFlag()
    {
        var dto = await _service.SavePresetAsync(CreateCommand("Default One", isDefault: true));

        dto.IsDefault.Should().BeTrue();

        var fromDb = await _service.GetPresetAsync(dto.Id);
        fromDb!.IsDefault.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
