using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class GenerationJobTests
{
    private static GenerationParameters ValidParameters => new()
    {
        PositivePrompt = "a beautiful landscape",
        CheckpointModelId = Guid.NewGuid()
    };

    [Fact]
    public void Create_WithValidParameters_SetsPropertiesCorrectly()
    {
        var projectId = Guid.NewGuid();
        var parameters = ValidParameters;

        var job = GenerationJob.Create(projectId, parameters);

        job.Id.Should().NotBeEmpty();
        job.ProjectId.Should().Be(projectId);
        job.Parameters.Should().Be(parameters);
        job.Status.Should().Be(GenerationJobStatus.Pending);
        job.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
        job.Images.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPrompt_ThrowsArgumentException(string? prompt)
    {
        var parameters = new GenerationParameters
        {
            PositivePrompt = prompt!,
            CheckpointModelId = Guid.NewGuid()
        };

        var act = () => GenerationJob.Create(Guid.NewGuid(), parameters);
        act.Should().Throw<ArgumentException>().WithMessage("*prompt*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(151)]
    public void Create_WithInvalidSteps_ThrowsArgumentException(int steps)
    {
        var parameters = ValidParameters with { Steps = steps };
        var act = () => GenerationJob.Create(Guid.NewGuid(), parameters);
        act.Should().Throw<ArgumentException>().WithMessage("*Steps*");
    }

    [Theory]
    [InlineData(100, 512)]
    [InlineData(512, 100)]
    [InlineData(513, 512)]
    public void Create_WithBadDimensions_ThrowsArgumentException(int width, int height)
    {
        var parameters = ValidParameters with { Width = width, Height = height };
        var act = () => GenerationJob.Create(Guid.NewGuid(), parameters);
        act.Should().Throw<ArgumentException>().WithMessage("*Width*height*");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(31)]
    public void Create_WithInvalidCfgScale_ThrowsArgumentException(double cfg)
    {
        var parameters = ValidParameters with { CfgScale = cfg };
        var act = () => GenerationJob.Create(Guid.NewGuid(), parameters);
        act.Should().Throw<ArgumentException>().WithMessage("*CFG*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Create_WithInvalidBatchSize_ThrowsArgumentException(int batchSize)
    {
        var parameters = ValidParameters with { BatchSize = batchSize };
        var act = () => GenerationJob.Create(Guid.NewGuid(), parameters);
        act.Should().Throw<ArgumentException>().WithMessage("*Batch*");
    }

    [Fact]
    public void Start_SetsStatusToRunningAndStartedAt()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Start();
        job.Status.Should().Be(GenerationJobStatus.Running);
        job.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Complete_SetsStatusToCompletedAndCompletedAt()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Start();
        job.Complete();
        job.Status.Should().Be(GenerationJobStatus.Completed);
        job.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Fail_SetsStatusToFailedWithErrorMessage()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Start();
        job.Fail("Something went wrong");
        job.Status.Should().Be(GenerationJobStatus.Failed);
        job.ErrorMessage.Should().Be("Something went wrong");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_SetsStatusToCancelled()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Cancel();
        job.Status.Should().Be(GenerationJobStatus.Cancelled);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddImage_AddsImageToCollection()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var image = GeneratedImage.Create(job.Id, "/path/to/image.png", 42, 512, 512, 1.5, "{}");

        job.AddImage(image);

        job.Images.Should().HaveCount(1);
        job.Images[0].Should().Be(image);
    }
}
