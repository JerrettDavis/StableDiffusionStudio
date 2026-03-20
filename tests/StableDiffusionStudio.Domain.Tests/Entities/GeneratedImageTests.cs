using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class GeneratedImageTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var jobId = Guid.NewGuid();
        var image = GeneratedImage.Create(jobId, "/images/test.png", 12345, 512, 768, 2.5, "{\"prompt\":\"test\"}");

        image.Id.Should().NotBeEmpty();
        image.GenerationJobId.Should().Be(jobId);
        image.FilePath.Should().Be("/images/test.png");
        image.Seed.Should().Be(12345);
        image.Width.Should().Be(512);
        image.Height.Should().Be(768);
        image.GenerationTimeSeconds.Should().Be(2.5);
        image.ParametersJson.Should().Be("{\"prompt\":\"test\"}");
        image.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        image.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void ToggleFavorite_SetsTrueWhenFalse()
    {
        var image = GeneratedImage.Create(Guid.NewGuid(), "/test.png", 1, 512, 512, 1.0, "{}");

        image.ToggleFavorite();

        image.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void ToggleFavorite_SetsFalseWhenTrue()
    {
        var image = GeneratedImage.Create(Guid.NewGuid(), "/test.png", 1, 512, 512, 1.0, "{}");

        image.ToggleFavorite();
        image.ToggleFavorite();

        image.IsFavorite.Should().BeFalse();
    }
}
