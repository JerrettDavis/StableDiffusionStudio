using FluentAssertions;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class InferenceSettingsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var settings = InferenceSettings.Default;

        settings.ThreadCount.Should().Be(0);
        settings.FlashAttention.Should().BeTrue();
        settings.DiffusionFlashAttention.Should().BeTrue();
        settings.VaeTiling.Should().BeTrue();
        settings.VaeDecodeOnly.Should().BeTrue();
        settings.KeepClipOnCPU.Should().BeTrue();
        settings.KeepVaeOnCPU.Should().BeTrue();
        settings.KeepControlNetOnCPU.Should().BeFalse();
        settings.OffloadParamsToCPU.Should().BeFalse();
        settings.EnableMmap.Should().BeFalse();
    }

    [Fact]
    public void EffectiveThreadCount_WhenZero_ReturnsProcessorCount()
    {
        var settings = new InferenceSettings { ThreadCount = 0 };
        settings.EffectiveThreadCount.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public void EffectiveThreadCount_WhenPositive_ReturnsSpecifiedValue()
    {
        var settings = new InferenceSettings { ThreadCount = 4 };
        settings.EffectiveThreadCount.Should().Be(4);
    }

    [Fact]
    public void LowVRAM_OffloadsEverythingToCPU()
    {
        var settings = InferenceSettings.LowVRAM;

        settings.KeepClipOnCPU.Should().BeTrue();
        settings.KeepVaeOnCPU.Should().BeTrue();
        settings.KeepControlNetOnCPU.Should().BeTrue();
        settings.OffloadParamsToCPU.Should().BeTrue();
        settings.VaeTiling.Should().BeTrue();
        settings.DiffusionFlashAttention.Should().BeTrue();
    }

    [Fact]
    public void HighVRAM_KeepsEverythingOnGPU()
    {
        var settings = InferenceSettings.HighVRAM;

        settings.KeepClipOnCPU.Should().BeFalse();
        settings.KeepVaeOnCPU.Should().BeFalse();
        settings.KeepControlNetOnCPU.Should().BeFalse();
        settings.OffloadParamsToCPU.Should().BeFalse();
        settings.VaeTiling.Should().BeFalse();
        settings.DiffusionFlashAttention.Should().BeTrue();
    }

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        var a = InferenceSettings.Default;
        var b = InferenceSettings.Default;
        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = InferenceSettings.Default;
        var modified = original with { ThreadCount = 8 };

        modified.ThreadCount.Should().Be(8);
        original.ThreadCount.Should().Be(0);
        modified.FlashAttention.Should().Be(original.FlashAttention);
    }
}
