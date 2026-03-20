using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class ModelFamilyPresetsTests
{
    [Fact]
    public void SD15_HasCorrectDefaults()
    {
        var preset = ModelFamilyPresets.SD15;
        preset.Family.Should().Be(ModelFamily.SD15);
        preset.Width.Should().Be(512);
        preset.Height.Should().Be(512);
        preset.Sampler.Should().Be(Sampler.EulerA);
        preset.Steps.Should().Be(20);
        preset.CfgScale.Should().Be(7.0);
    }

    [Fact]
    public void SDXL_HasCorrectDefaults()
    {
        var preset = ModelFamilyPresets.SDXL;
        preset.Family.Should().Be(ModelFamily.SDXL);
        preset.Width.Should().Be(1024);
        preset.Height.Should().Be(1024);
        preset.Sampler.Should().Be(Sampler.DPMPlusPlus2MKarras);
        preset.Scheduler.Should().Be(Scheduler.Karras);
        preset.Steps.Should().Be(25);
    }

    [Fact]
    public void Flux_HasLowCfg()
    {
        var preset = ModelFamilyPresets.Flux;
        preset.Family.Should().Be(ModelFamily.Flux);
        preset.CfgScale.Should().Be(3.5);
        preset.Width.Should().Be(1024);
        preset.Height.Should().Be(1024);
        preset.NegativePrompt.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ModelFamily.SD15, 512, 512)]
    [InlineData(ModelFamily.SDXL, 1024, 1024)]
    [InlineData(ModelFamily.Flux, 1024, 1024)]
    [InlineData(ModelFamily.Unknown, 512, 512)]
    public void GetPreset_ReturnsCorrectResolution(ModelFamily family, int expectedWidth, int expectedHeight)
    {
        var preset = ModelFamilyPresets.GetPreset(family);
        preset.Width.Should().Be(expectedWidth);
        preset.Height.Should().Be(expectedHeight);
    }

    [Fact]
    public void All_ContainsAllPresets()
    {
        ModelFamilyPresets.All.Should().HaveCount(3);
        ModelFamilyPresets.All.Should().Contain(p => p.Family == ModelFamily.SD15);
        ModelFamilyPresets.All.Should().Contain(p => p.Family == ModelFamily.SDXL);
        ModelFamilyPresets.All.Should().Contain(p => p.Family == ModelFamily.Flux);
    }
}
