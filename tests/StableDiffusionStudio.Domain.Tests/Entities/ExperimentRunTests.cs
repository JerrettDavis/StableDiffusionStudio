using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ExperimentRunTests
{
    private static readonly Guid ExperimentId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInputs_SetsPendingStatus()
    {
        var run = ExperimentRun.Create(ExperimentId, 6, 42L);

        run.Id.Should().NotBeEmpty();
        run.ExperimentId.Should().Be(ExperimentId);
        run.TotalCombinations.Should().Be(6);
        run.FixedSeed.Should().Be(42L);
        run.UseFixedSeed.Should().BeTrue();
        run.CompletedCount.Should().Be(0);
        run.Status.Should().Be(ExperimentRunStatus.Pending);
        run.ErrorMessage.Should().BeNull();
        run.StartedAt.Should().BeNull();
        run.CompletedAt.Should().BeNull();
        run.Images.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithZeroOrNegativeCombinations_ThrowsArgumentException(int totalCombinations)
    {
        var act = () => ExperimentRun.Create(ExperimentId, totalCombinations, 42L);
        act.Should().Throw<ArgumentException>().WithMessage("*combinations*");
    }

    [Fact]
    public void Create_WithUseFixedSeedFalse_SetsUseFixedSeedFalse()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 0L, useFixedSeed: false);
        run.UseFixedSeed.Should().BeFalse();
    }

    [Fact]
    public void Start_SetsStatusToRunningAndSetsStartedAt()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 42L);

        run.Start();

        run.Status.Should().Be(ExperimentRunStatus.Running);
        run.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        run.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Complete_SetsStatusToCompletedAndSetsCompletedAt()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 42L);
        run.Start();

        run.Complete();

        run.Status.Should().Be(ExperimentRunStatus.Completed);
        run.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Fail_SetsStatusToFailedAndStoresErrorMessage()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 42L);
        run.Start();

        run.Fail("Out of VRAM");

        run.Status.Should().Be(ExperimentRunStatus.Failed);
        run.ErrorMessage.Should().Be("Out of VRAM");
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_SetsStatusToCancelled()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 42L);
        run.Start();

        run.Cancel();

        run.Status.Should().Be(ExperimentRunStatus.Cancelled);
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void IncrementCompleted_IncreasesCompletedCount()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 42L);

        run.IncrementCompleted();
        run.IncrementCompleted();

        run.CompletedCount.Should().Be(2);
    }

    [Fact]
    public void AddImage_AddsImageToCollection()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 42L);
        var image = ExperimentRunImage.Create(run.Id, "/path/img.png", 42L, 1.5, "{}", 0, 0);

        run.AddImage(image);

        run.Images.Should().HaveCount(1);
        run.Images[0].Should().BeSameAs(image);
    }

    [Fact]
    public void AddImage_MultipleImages_AllAdded()
    {
        var run = ExperimentRun.Create(ExperimentId, 4, 42L);
        var image1 = ExperimentRunImage.Create(run.Id, "/img1.png", 1L, 1.0, "{}", 0, 0);
        var image2 = ExperimentRunImage.Create(run.Id, "/img2.png", 2L, 1.0, "{}", 0, 1);
        var image3 = ExperimentRunImage.Create(run.Id, "/img3.png", 3L, 1.0, "{}", 1, 0);

        run.AddImage(image1);
        run.AddImage(image2);
        run.AddImage(image3);

        run.Images.Should().HaveCount(3);
    }

    // ExperimentRunImage tests

    [Fact]
    public void ExperimentRunImage_Create_SetsPropertiesCorrectly()
    {
        var runId = Guid.NewGuid();
        var image = ExperimentRunImage.Create(runId, "/path/to/img.png", 12345L, 2.3, "{\"cfg\":7}", 1, 2);

        image.Id.Should().NotBeEmpty();
        image.RunId.Should().Be(runId);
        image.FilePath.Should().Be("/path/to/img.png");
        image.Seed.Should().Be(12345L);
        image.GenerationTimeSeconds.Should().Be(2.3);
        image.AxisValuesJson.Should().Be("{\"cfg\":7}");
        image.GridX.Should().Be(1);
        image.GridY.Should().Be(2);
        image.IsWinner.Should().BeFalse();
        image.ContentRating.Should().Be(ContentRating.Unknown);
        image.NsfwScore.Should().Be(0.0);
    }

    [Fact]
    public void ExperimentRunImage_MarkAsWinner_SetsIsWinnerTrue()
    {
        var image = ExperimentRunImage.Create(Guid.NewGuid(), "/img.png", 1L, 1.0, "{}", 0, 0);

        image.MarkAsWinner();

        image.IsWinner.Should().BeTrue();
    }

    [Fact]
    public void ExperimentRunImage_UnmarkAsWinner_SetsIsWinnerFalse()
    {
        var image = ExperimentRunImage.Create(Guid.NewGuid(), "/img.png", 1L, 1.0, "{}", 0, 0);
        image.MarkAsWinner();

        image.UnmarkAsWinner();

        image.IsWinner.Should().BeFalse();
    }

    [Fact]
    public void ExperimentRunImage_WinnerToggle_RoundTrips()
    {
        var image = ExperimentRunImage.Create(Guid.NewGuid(), "/img.png", 1L, 1.0, "{}", 0, 0);

        image.MarkAsWinner();
        image.IsWinner.Should().BeTrue();

        image.UnmarkAsWinner();
        image.IsWinner.Should().BeFalse();

        image.MarkAsWinner();
        image.IsWinner.Should().BeTrue();
    }

    [Fact]
    public void ExperimentRunImage_SetContentRating_UpdatesRatingAndScore()
    {
        var image = ExperimentRunImage.Create(Guid.NewGuid(), "/img.png", 1L, 1.0, "{}", 0, 0);

        image.SetContentRating(ContentRating.Safe, 0.02);

        image.ContentRating.Should().Be(ContentRating.Safe);
        image.NsfwScore.Should().Be(0.02);
    }
}
