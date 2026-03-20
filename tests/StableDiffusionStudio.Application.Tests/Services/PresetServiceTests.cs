using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Tests.Services;

public class PresetServiceTests
{
    private readonly IPresetRepository _repo = Substitute.For<IPresetRepository>();
    private readonly PresetService _service;

    public PresetServiceTests() { _service = new PresetService(_repo); }

    [Fact]
    public async Task SavePresetAsync_NewPreset_CreatesAndReturnsDto()
    {
        var command = new SavePresetCommand(
            Id: null, Name: "My Preset", Description: "Desc",
            AssociatedModelId: null, ModelFamilyFilter: ModelFamily.SDXL,
            IsDefault: false, PositivePromptTemplate: null,
            NegativePrompt: "ugly", Sampler: Sampler.DPMPlusPlus2MKarras,
            Scheduler: Scheduler.Karras, Steps: 25, CfgScale: 7.0,
            Width: 1024, Height: 1024, BatchSize: 1, ClipSkip: 1);

        var result = await _service.SavePresetAsync(command);

        result.Should().NotBeNull();
        result.Name.Should().Be("My Preset");
        result.ModelFamilyFilter.Should().Be(ModelFamily.SDXL);
        result.Steps.Should().Be(25);
        await _repo.Received(1).AddAsync(Arg.Any<GenerationPresetEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavePresetAsync_ExistingPreset_UpdatesAndReturnsDto()
    {
        var existing = GenerationPresetEntity.Create(
            "Old", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        _repo.GetByIdAsync(existing.Id).Returns(existing);

        var command = new SavePresetCommand(
            Id: existing.Id, Name: "Updated", Description: "New desc",
            AssociatedModelId: null, ModelFamilyFilter: null,
            IsDefault: true, PositivePromptTemplate: null,
            NegativePrompt: "bad", Sampler: Sampler.DDIM,
            Scheduler: Scheduler.Normal, Steps: 30, CfgScale: 5.0,
            Width: 768, Height: 768, BatchSize: 2, ClipSkip: 2);

        var result = await _service.SavePresetAsync(command);

        result.Name.Should().Be("Updated");
        result.Steps.Should().Be(30);
        result.IsDefault.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().AddAsync(Arg.Any<GenerationPresetEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListPresetsAsync_DelegatesToRepository()
    {
        var presets = new List<GenerationPresetEntity>
        {
            GenerationPresetEntity.Create("A", null, null, ModelFamily.SDXL, null, "", Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1),
            GenerationPresetEntity.Create("B", null, null, null, null, "", Sampler.Euler, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1)
        };
        _repo.ListAsync(null, ModelFamily.SDXL, Arg.Any<CancellationToken>()).Returns(presets);

        var results = await _service.ListPresetsAsync(family: ModelFamily.SDXL);

        results.Should().HaveCount(2);
        await _repo.Received(1).ListAsync(null, ModelFamily.SDXL, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePresetAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        await _service.DeletePresetAsync(id);
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPresetAsync_WhenExists_ReturnsDto()
    {
        var preset = GenerationPresetEntity.Create(
            "Test", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        _repo.GetByIdAsync(preset.Id).Returns(preset);

        var result = await _service.GetPresetAsync(preset.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
    }

    [Fact]
    public async Task GetPresetAsync_WhenNotExists_ReturnsNull()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>()).Returns((GenerationPresetEntity?)null);
        var result = await _service.GetPresetAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApplyPresetToParameters_WhenExists_ReturnsDto()
    {
        var preset = GenerationPresetEntity.Create(
            "Apply Me", null, null, ModelFamily.Flux, null, "",
            Sampler.Euler, Scheduler.Normal, 20, 3.5, 1024, 1024, 1, 1);
        _repo.GetByIdAsync(preset.Id).Returns(preset);

        var result = await _service.ApplyPresetToParameters(preset.Id);

        result.Should().NotBeNull();
        result!.Sampler.Should().Be(Sampler.Euler);
        result.CfgScale.Should().Be(3.5);
    }

    [Fact]
    public async Task ListPresetsAsync_WithModelId_DelegatesToRepository()
    {
        var modelId = Guid.NewGuid();
        _repo.ListAsync(modelId, null, Arg.Any<CancellationToken>())
            .Returns(new List<GenerationPresetEntity>());

        await _service.ListPresetsAsync(modelId: modelId);

        await _repo.Received(1).ListAsync(modelId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavePresetAsync_ExistingPresetNotFound_CreatesNew()
    {
        var nonExistentId = Guid.NewGuid();
        _repo.GetByIdAsync(nonExistentId).Returns((GenerationPresetEntity?)null);

        var command = new SavePresetCommand(
            Id: nonExistentId, Name: "New", Description: null,
            AssociatedModelId: null, ModelFamilyFilter: null,
            IsDefault: false, PositivePromptTemplate: null,
            NegativePrompt: "", Sampler: Sampler.EulerA,
            Scheduler: Scheduler.Normal, Steps: 20, CfgScale: 7.0,
            Width: 512, Height: 512, BatchSize: 1, ClipSkip: 1);

        var result = await _service.SavePresetAsync(command);

        result.Should().NotBeNull();
        await _repo.Received(1).AddAsync(Arg.Any<GenerationPresetEntity>(), Arg.Any<CancellationToken>());
    }
}
