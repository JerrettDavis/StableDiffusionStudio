using FluentAssertions;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class OutputSettingsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        var settings = OutputSettings.Default;
        settings.CustomOutputDirectory.Should().BeNull();
        settings.FilenamePattern.Should().Be("[seed]");
        settings.AutoSave.Should().BeTrue();
        settings.SaveGrid.Should().BeFalse();
    }

    [Fact]
    public void FormatFilename_SeedOnly_ProducesSeedPng()
    {
        var settings = new OutputSettings { FilenamePattern = "[seed]" };
        var result = settings.FormatFilename(12345, "a cat", DateTimeOffset.UtcNow, "sd15");
        result.Should().Be("12345.png");
    }

    [Fact]
    public void FormatFilename_SeedAndDate_ProducesExpected()
    {
        var settings = new OutputSettings { FilenamePattern = "[seed]-[date]" };
        var date = new DateTimeOffset(2026, 3, 21, 14, 30, 22, TimeSpan.Zero);
        var result = settings.FormatFilename(42, "test", date, "model");
        result.Should().Be("42-20260321-143022.png");
    }

    [Fact]
    public void FormatFilename_AllTokens_ProducesExpected()
    {
        var settings = new OutputSettings { FilenamePattern = "[seed]-[prompt]-[date]-[model]" };
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = settings.FormatFilename(1, "cat", date, "sd15");
        result.Should().Be("1-cat-20260101-000000-sd15.png");
    }

    [Fact]
    public void FormatFilename_LongPrompt_IsTruncated()
    {
        var settings = new OutputSettings { FilenamePattern = "[prompt]" };
        var longPrompt = new string('a', 100);
        var result = settings.FormatFilename(1, longPrompt, DateTimeOffset.UtcNow, "model");
        // 50 chars + ".png"
        result.Length.Should().BeLessThanOrEqualTo(54);
    }

    [Fact]
    public void FormatFilename_InvalidCharsInPrompt_AreSanitized()
    {
        var settings = new OutputSettings { FilenamePattern = "[prompt]" };
        var result = settings.FormatFilename(1, "a/cat\\in:space", DateTimeOffset.UtcNow, "model");
        result.Should().NotContain("/");
        result.Should().NotContain("\\");
        result.Should().NotContain(":");
        result.Should().EndWith(".png");
    }

    [Fact]
    public void FormatFilename_InvalidCharsInModel_AreSanitized()
    {
        var settings = new OutputSettings { FilenamePattern = "[model]" };
        var result = settings.FormatFilename(1, "test", DateTimeOffset.UtcNow, "my/model:v1");
        result.Should().NotContain("/");
        result.Should().NotContain(":");
        result.Should().EndWith(".png");
    }

    [Fact]
    public void FormatFilename_NoTokens_ReturnPatternWithPng()
    {
        var settings = new OutputSettings { FilenamePattern = "output" };
        var result = settings.FormatFilename(1, "test", DateTimeOffset.UtcNow, "model");
        result.Should().Be("output.png");
    }

    [Fact]
    public void Record_Equality_Works()
    {
        var a = new OutputSettings { FilenamePattern = "[seed]", AutoSave = true };
        var b = new OutputSettings { FilenamePattern = "[seed]", AutoSave = true };
        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = OutputSettings.Default;
        var modified = original with { AutoSave = false, FilenamePattern = "[date]-[seed]" };
        modified.AutoSave.Should().BeFalse();
        modified.FilenamePattern.Should().Be("[date]-[seed]");
        original.AutoSave.Should().BeTrue();
    }
}
