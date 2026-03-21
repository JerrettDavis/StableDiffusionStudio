using FluentAssertions;
using StableDiffusionStudio.Infrastructure.Services;

namespace StableDiffusionStudio.Infrastructure.Tests.Services;

public class A1111ParameterParserTests
{
    [Fact]
    public void Parse_FullA1111String_ExtractsAllFields()
    {
        var text = """
            masterpiece, best quality, 1girl
            Negative prompt: ugly, blurry, lowres
            Steps: 20, Sampler: Euler a, CFG scale: 7, Seed: 12345, Size: 512x768, Model: v1-5-pruned, Clip skip: 2
            """;

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.PositivePrompt.Should().Be("masterpiece, best quality, 1girl");
        result.NegativePrompt.Should().Be("ugly, blurry, lowres");
        result.Steps.Should().Be(20);
        result.Sampler.Should().Be("Euler a");
        result.CfgScale.Should().Be(7.0);
        result.Seed.Should().Be(12345);
        result.Width.Should().Be(512);
        result.Height.Should().Be(768);
        result.Model.Should().Be("v1-5-pruned");
        result.ClipSkip.Should().Be(2);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        A1111ParameterParser.Parse("").Should().BeNull();
    }

    [Fact]
    public void Parse_NullString_ReturnsNull()
    {
        A1111ParameterParser.Parse(null!).Should().BeNull();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsNull()
    {
        A1111ParameterParser.Parse("   \n\t  ").Should().BeNull();
    }

    [Fact]
    public void Parse_MissingNegativePrompt_ReturnsEmptyNegative()
    {
        var text = """
            a beautiful landscape
            Steps: 30, Sampler: DPM++ 2M, CFG scale: 7.5, Seed: 999, Size: 1024x1024
            """;

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.PositivePrompt.Should().Be("a beautiful landscape");
        result.NegativePrompt.Should().BeEmpty();
        result.Steps.Should().Be(30);
        result.Seed.Should().Be(999);
    }

    [Fact]
    public void Parse_MissingStepsLine_ReturnsNullFields()
    {
        var text = "just a prompt with no parameters";

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.PositivePrompt.Should().Be("just a prompt with no parameters");
        result.Steps.Should().BeNull();
        result.Sampler.Should().BeNull();
        result.CfgScale.Should().BeNull();
        result.Seed.Should().BeNull();
        result.Width.Should().BeNull();
        result.Height.Should().BeNull();
    }

    [Fact]
    public void Parse_MultilinePositivePrompt_CombinesLines()
    {
        var text = "line one\nline two\nNegative prompt: bad\nSteps: 10, Seed: 1, Size: 512x512";

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.PositivePrompt.Should().Be("line one\nline two");
        result.NegativePrompt.Should().Be("bad");
    }

    [Fact]
    public void Parse_MultilineNegativePrompt_CombinesLines()
    {
        var text = "prompt\nNegative prompt: ugly,\nbad, blurry\nSteps: 20, Seed: 1, Size: 512x512";

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.NegativePrompt.Should().Be("ugly, bad, blurry");
    }

    [Fact]
    public void Parse_MissingModel_ReturnsNullModel()
    {
        var text = "test prompt\nSteps: 20, Sampler: Euler, CFG scale: 7, Seed: 42, Size: 512x512";

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.Model.Should().BeNull();
        result.ClipSkip.Should().BeNull();
    }

    [Fact]
    public void Parse_DecimalCfgScale_ParsesCorrectly()
    {
        var text = "prompt\nSteps: 20, CFG scale: 3.5, Seed: 1, Size: 512x512";

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.CfgScale.Should().Be(3.5);
    }

    [Fact]
    public void Parse_LargeSeed_ParsesCorrectly()
    {
        var text = "prompt\nSteps: 20, Seed: 4294967295, Size: 512x512";

        var result = A1111ParameterParser.Parse(text);

        result.Should().NotBeNull();
        result!.Seed.Should().Be(4294967295);
    }
}
