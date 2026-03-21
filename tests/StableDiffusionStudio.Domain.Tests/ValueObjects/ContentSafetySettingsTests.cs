using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class ContentSafetySettingsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var settings = ContentSafetySettings.Default;

        settings.FilterMode.Should().Be(NsfwFilterMode.Off);
        settings.NsfwThreshold.Should().Be(0.5);
        settings.QuestionableThreshold.Should().Be(0.3);
        settings.ScanExistingOnEnable.Should().BeFalse();
    }

    [Fact]
    public void WithModifiedFilterMode_CreatesNewInstance()
    {
        var original = ContentSafetySettings.Default;
        var modified = original with { FilterMode = NsfwFilterMode.Blur };

        modified.FilterMode.Should().Be(NsfwFilterMode.Blur);
        original.FilterMode.Should().Be(NsfwFilterMode.Off);
    }

    [Fact]
    public void WithModifiedThresholds_CreatesNewInstance()
    {
        var settings = ContentSafetySettings.Default with
        {
            NsfwThreshold = 0.7,
            QuestionableThreshold = 0.4
        };

        settings.NsfwThreshold.Should().Be(0.7);
        settings.QuestionableThreshold.Should().Be(0.4);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = ContentSafetySettings.Default;
        var b = ContentSafetySettings.Default;

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = ContentSafetySettings.Default;
        var b = a with { FilterMode = NsfwFilterMode.BlockAndDelete };

        a.Should().NotBe(b);
    }
}
