using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class GenerationPresetEntityTests
{
    [Fact]
    public void Create_WithValidName_SetsPropertiesCorrectly()
    {
        var preset = GenerationPresetEntity.Create(
            "My Preset", "A description",
            Guid.NewGuid(), ModelFamily.SDXL,
            "a photo of {subject}", "ugly, blurry",
            Sampler.DPMPlusPlus2MKarras, Scheduler.Karras,
            25, 7.0, 1024, 1024, 1, 1);

        preset.Id.Should().NotBeEmpty();
        preset.Name.Should().Be("My Preset");
        preset.Description.Should().Be("A description");
        preset.Sampler.Should().Be(Sampler.DPMPlusPlus2MKarras);
        preset.Scheduler.Should().Be(Scheduler.Karras);
        preset.Steps.Should().Be(25);
        preset.CfgScale.Should().Be(7.0);
        preset.Width.Should().Be(1024);
        preset.Height.Should().Be(1024);
        preset.NegativePrompt.Should().Be("ugly, blurry");
        preset.PositivePromptTemplate.Should().Be("a photo of {subject}");
        preset.IsDefault.Should().BeFalse();
        preset.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        preset.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        var act = () => GenerationPresetEntity.Create(
            name!, null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TrimsName()
    {
        var preset = GenerationPresetEntity.Create(
            "  Spaced Name  ", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        preset.Name.Should().Be("Spaced Name");
    }

    [Fact]
    public void Create_WithNullAssociatedModelId_IsUniversalPreset()
    {
        var preset = GenerationPresetEntity.Create(
            "Universal", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        preset.AssociatedModelId.Should().BeNull();
        preset.ModelFamilyFilter.Should().BeNull();
    }

    [Fact]
    public void Update_ChangesAllProperties()
    {
        var preset = GenerationPresetEntity.Create(
            "Original", "Desc", null, null, null, "bad",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        var modelId = Guid.NewGuid();
        preset.Update(
            "Updated", "New Desc",
            modelId, ModelFamily.Flux,
            "template", "ugly",
            Sampler.Euler, Scheduler.Exponential,
            30, 3.5, 1024, 768, 2, 2);

        preset.Name.Should().Be("Updated");
        preset.Description.Should().Be("New Desc");
        preset.AssociatedModelId.Should().Be(modelId);
        preset.ModelFamilyFilter.Should().Be(ModelFamily.Flux);
        preset.PositivePromptTemplate.Should().Be("template");
        preset.NegativePrompt.Should().Be("ugly");
        preset.Sampler.Should().Be(Sampler.Euler);
        preset.Scheduler.Should().Be(Scheduler.Exponential);
        preset.Steps.Should().Be(30);
        preset.CfgScale.Should().Be(3.5);
        preset.Width.Should().Be(1024);
        preset.Height.Should().Be(768);
        preset.BatchSize.Should().Be(2);
        preset.ClipSkip.Should().Be(2);
        preset.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithInvalidName_ThrowsArgumentException(string? name)
    {
        var preset = GenerationPresetEntity.Create(
            "Valid", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        var act = () => preset.Update(
            name!, null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetDefault_SetsIsDefaultAndUpdatesTimestamp()
    {
        var preset = GenerationPresetEntity.Create(
            "Test", null, null, null, null, "",
            Sampler.EulerA, Scheduler.Normal, 20, 7.0, 512, 512, 1, 1);

        preset.IsDefault.Should().BeFalse();

        preset.SetDefault(true);
        preset.IsDefault.Should().BeTrue();
        preset.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));

        preset.SetDefault(false);
        preset.IsDefault.Should().BeFalse();
    }
}
