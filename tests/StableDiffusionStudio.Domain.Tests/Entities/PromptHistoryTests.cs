using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class PromptHistoryTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var entry = PromptHistory.Create("a beautiful sunset", "ugly, blurry");

        entry.Id.Should().NotBeEmpty();
        entry.PositivePrompt.Should().Be("a beautiful sunset");
        entry.NegativePrompt.Should().Be("ugly, blurry");
        entry.UseCount.Should().Be(1);
        entry.UsedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithNullNegativePrompt_SetsEmpty()
    {
        var entry = PromptHistory.Create("a test prompt", null!);

        entry.NegativePrompt.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyPositivePrompt_Throws()
    {
        var act = () => PromptHistory.Create("", "negative");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithWhitespacePositivePrompt_Throws()
    {
        var act = () => PromptHistory.Create("   ", "negative");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IncrementUsage_IncrementsCountAndUpdatesTimestamp()
    {
        var entry = PromptHistory.Create("test prompt", "negative");
        var originalTimestamp = entry.UsedAt;
        entry.UseCount.Should().Be(1);

        entry.IncrementUsage();

        entry.UseCount.Should().Be(2);
        entry.UsedAt.Should().BeOnOrAfter(originalTimestamp);
    }

    [Fact]
    public void IncrementUsage_MultipleTimes_AccumulatesCount()
    {
        var entry = PromptHistory.Create("test prompt", "");

        entry.IncrementUsage();
        entry.IncrementUsage();
        entry.IncrementUsage();

        entry.UseCount.Should().Be(4);
    }
}
