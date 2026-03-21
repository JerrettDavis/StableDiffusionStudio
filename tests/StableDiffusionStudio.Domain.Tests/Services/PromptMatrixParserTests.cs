using FluentAssertions;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class PromptMatrixParserTests
{
    [Fact]
    public void SinglePrompt_ReturnsUnchanged()
    {
        var result = PromptMatrixParser.ExpandMatrix("a beautiful landscape");
        result.Should().HaveCount(1);
        result[0].Should().Be("a beautiful landscape");
    }

    [Fact]
    public void TwoSegments_ReturnsTwoPrompts()
    {
        var result = PromptMatrixParser.ExpandMatrix("a cat | a dog");
        result.Should().HaveCount(2);
        result[0].Should().Be("a cat");
        result[1].Should().Be("a dog");
    }

    [Fact]
    public void ThreeSegments_ReturnsThreePrompts()
    {
        var result = PromptMatrixParser.ExpandMatrix("a cat | a dog | a bird");
        result.Should().HaveCount(3);
        result[0].Should().Be("a cat");
        result[1].Should().Be("a dog");
        result[2].Should().Be("a bird");
    }

    [Fact]
    public void EmptySegments_AreSkipped()
    {
        var result = PromptMatrixParser.ExpandMatrix("a cat | | a dog");
        result.Should().HaveCount(2);
        result[0].Should().Be("a cat");
        result[1].Should().Be("a dog");
    }

    [Fact]
    public void WhitespaceSegments_AreSkipped()
    {
        var result = PromptMatrixParser.ExpandMatrix("a cat |   | a dog");
        result.Should().HaveCount(2);
        result[0].Should().Be("a cat");
        result[1].Should().Be("a dog");
    }

    [Fact]
    public void NoPipes_ReturnsSinglePrompt()
    {
        var result = PromptMatrixParser.ExpandMatrix("just a normal prompt");
        result.Should().HaveCount(1);
        result[0].Should().Be("just a normal prompt");
    }

    [Fact]
    public void EmptyString_ReturnsSingleEmpty()
    {
        var result = PromptMatrixParser.ExpandMatrix("");
        result.Should().HaveCount(1);
        result[0].Should().Be("");
    }

    [Fact]
    public void WhitespaceOnly_ReturnsSingleWhitespace()
    {
        var result = PromptMatrixParser.ExpandMatrix("   ");
        result.Should().HaveCount(1);
        result[0].Should().Be("   ");
    }

    [Fact]
    public void SegmentsAreTrimmed()
    {
        var result = PromptMatrixParser.ExpandMatrix("  cat  |  dog  ");
        result.Should().HaveCount(2);
        result[0].Should().Be("cat");
        result[1].Should().Be("dog");
    }

    [Fact]
    public void SinglePipeWithEmptyRight_ReturnsLeftOnly()
    {
        var result = PromptMatrixParser.ExpandMatrix("a cat |");
        result.Should().HaveCount(1);
        result[0].Should().Be("a cat");
    }

    [Fact]
    public void AllEmptySegments_ReturnsOriginal()
    {
        var result = PromptMatrixParser.ExpandMatrix("| | |");
        result.Should().HaveCount(1);
        result[0].Should().Be("| | |");
    }
}
