using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Enums;

public class GenerationModeTests
{
    [Fact]
    public void GenerationMode_HasTextToImage()
    {
        GenerationMode.TextToImage.Should().BeDefined();
    }

    [Fact]
    public void GenerationMode_HasImageToImage()
    {
        GenerationMode.ImageToImage.Should().BeDefined();
    }

    [Fact]
    public void GenerationMode_TextToImage_IsDefault()
    {
        default(GenerationMode).Should().Be(GenerationMode.TextToImage);
    }

    [Fact]
    public void GenerationMode_HasExactlyTwoValues()
    {
        Enum.GetValues<GenerationMode>().Should().HaveCount(2);
    }
}
