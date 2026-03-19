using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class ModelFileAnalyzerTests
{
    [Theory]
    [InlineData("model.safetensors", ModelFormat.SafeTensors)]
    [InlineData("model.ckpt", ModelFormat.CKPT)]
    [InlineData("model.gguf", ModelFormat.GGUF)]
    [InlineData("model.bin", ModelFormat.Unknown)]
    [InlineData("model.txt", ModelFormat.Unknown)]
    public void InferFormat_FromExtension_ReturnsCorrectFormat(string fileName, ModelFormat expected)
    {
        var info = new ModelFileInfo(fileName, 1000, null);

        ModelFileAnalyzer.InferFormat(info).Should().Be(expected);
    }

    [Theory]
    [InlineData("model.safetensors", 2_000_000_000L, ModelFamily.SD15)]
    [InlineData("model.safetensors", 4_200_000_000L, ModelFamily.SD15)]
    [InlineData("model.safetensors", 6_500_000_000L, ModelFamily.SDXL)]
    [InlineData("model.safetensors", 7_200_000_000L, ModelFamily.SDXL)]
    [InlineData("model.safetensors", 12_000_000_000L, ModelFamily.Flux)]
    [InlineData("model.safetensors", 500_000_000L, ModelFamily.Unknown)]
    public void InferFamily_FromSizeHeuristic_ReturnsCorrectFamily(string fileName, long fileSize, ModelFamily expected)
    {
        var info = new ModelFileInfo(fileName, fileSize, null);

        ModelFileAnalyzer.InferFamily(info).Should().Be(expected);
    }

    [Fact]
    public void InferFamily_WithHeaderHint_OverridesSizeHeuristic()
    {
        var info = new ModelFileInfo("model.safetensors", 2_000_000_000L,
            HeaderHint: "conditioner.embedders.1.model.transformer");

        ModelFileAnalyzer.InferFamily(info).Should().Be(ModelFamily.SDXL);
    }
}
