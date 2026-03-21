using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

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
        image.ContentRating.Should().Be(ContentRating.Unknown);
        image.NsfwScore.Should().Be(0);
        image.IsRevealed.Should().BeFalse();
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

    [Fact]
    public void SetContentRating_UpdatesRatingAndScore()
    {
        var image = GeneratedImage.Create(Guid.NewGuid(), "/test.png", 1, 512, 512, 1.0, "{}");

        image.SetContentRating(ContentRating.Nsfw, 0.85);

        image.ContentRating.Should().Be(ContentRating.Nsfw);
        image.NsfwScore.Should().Be(0.85);
    }

    [Fact]
    public void SetContentRating_CanChangeRating()
    {
        var image = GeneratedImage.Create(Guid.NewGuid(), "/test.png", 1, 512, 512, 1.0, "{}");

        image.SetContentRating(ContentRating.Nsfw, 0.9);
        image.SetContentRating(ContentRating.Safe, 0.1);

        image.ContentRating.Should().Be(ContentRating.Safe);
        image.NsfwScore.Should().Be(0.1);
    }

    [Fact]
    public void Reveal_SetsIsRevealedTrue()
    {
        var image = GeneratedImage.Create(Guid.NewGuid(), "/test.png", 1, 512, 512, 1.0, "{}");

        image.Reveal();

        image.IsRevealed.Should().BeTrue();
    }

    [Fact]
    public void Conceal_SetsIsRevealedFalse()
    {
        var image = GeneratedImage.Create(Guid.NewGuid(), "/test.png", 1, 512, 512, 1.0, "{}");

        image.Reveal();
        image.Conceal();

        image.IsRevealed.Should().BeFalse();
    }
}
