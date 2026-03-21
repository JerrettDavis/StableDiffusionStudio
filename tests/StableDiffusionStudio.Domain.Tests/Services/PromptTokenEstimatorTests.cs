using FluentAssertions;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class PromptTokenEstimatorTests
{
    [Fact]
    public void EstimateTokens_EmptyString_ReturnsZero()
    {
        PromptTokenEstimator.EstimateTokens("").Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_NullString_ReturnsZero()
    {
        PromptTokenEstimator.EstimateTokens(null!).Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_WhitespaceOnly_ReturnsZero()
    {
        PromptTokenEstimator.EstimateTokens("   \t\n  ").Should().Be(0);
    }

    [Fact]
    public void EstimateTokens_SingleWord_ReturnsExpected()
    {
        var result = PromptTokenEstimator.EstimateTokens("cat");
        result.Should().Be(1); // 1 * 1.3 = 1.3 -> truncated to 1
    }

    [Fact]
    public void EstimateTokens_MultipleWords_ReturnsEstimate()
    {
        // 10 words * 1.3 = 13
        var result = PromptTokenEstimator.EstimateTokens("a beautiful photo of a cat sitting on a table");
        result.Should().Be(13);
    }

    [Fact]
    public void EstimateTokens_LongPrompt_ExceedsLimit()
    {
        // 60 words * 1.3 = 78 -> exceeds 77 SD 1.5 limit
        var words = string.Join(" ", Enumerable.Repeat("word", 60));
        var result = PromptTokenEstimator.EstimateTokens(words);
        result.Should().Be(78);
    }

    [Fact]
    public void EstimateTokens_SpecialCharacters_SplitsCorrectly()
    {
        // Commas and dots split words: "a, beautiful. photo" = 3 words
        var result = PromptTokenEstimator.EstimateTokens("a, beautiful. photo");
        result.Should().Be(3); // 3 * 1.3 = 3.9 -> 3
    }

    [Fact]
    public void EstimateTokens_CommaDelimited_SplitsCorrectly()
    {
        var result = PromptTokenEstimator.EstimateTokens("masterpiece, best quality, 1girl, solo");
        result.Should().Be((int)(5 * 1.3)); // 5 words after split
    }

    [Fact]
    public void GetTokenLimit_SD15_Returns77()
    {
        PromptTokenEstimator.GetTokenLimit("SD15").Should().Be(77);
    }

    [Fact]
    public void GetTokenLimit_SDXL_Returns154()
    {
        PromptTokenEstimator.GetTokenLimit("SDXL").Should().Be(154);
    }

    [Fact]
    public void GetTokenLimit_Flux_Returns154()
    {
        PromptTokenEstimator.GetTokenLimit("Flux").Should().Be(154);
    }

    [Fact]
    public void GetTokenLimit_Null_Returns77()
    {
        PromptTokenEstimator.GetTokenLimit(null).Should().Be(77);
    }

    [Fact]
    public void GetTokenLimit_Unknown_Returns77()
    {
        PromptTokenEstimator.GetTokenLimit("UnknownModel").Should().Be(77);
    }
}
