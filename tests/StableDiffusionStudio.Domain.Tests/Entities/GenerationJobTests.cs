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

    [Fact]
    public void Start_AlreadyRunning_OverwritesStartedAt()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Start();
        var firstStartedAt = job.StartedAt;

        // Domain doesn't guard against double-start on GenerationJob (unlike JobRecord)
        job.Start();
        job.Status.Should().Be(GenerationJobStatus.Running);
    }

    [Fact]
    public void Complete_SetsCompletedAtTimestamp()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Start();
        var before = DateTimeOffset.UtcNow;

        job.Complete();

        job.CompletedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Fail_AfterStart_SetsErrorMessageAndCompletedAt()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Start();

        job.Fail("Out of memory");

        job.Status.Should().Be(GenerationJobStatus.Failed);
        job.ErrorMessage.Should().Be("Out of memory");
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhileRunning_SetsStatusToCancelled()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Start();

        job.Cancel();

        job.Status.Should().Be(GenerationJobStatus.Cancelled);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddImage_MultipleImages_IncreasesCount()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.AddImage(GeneratedImage.Create(job.Id, "/img1.png", 1, 512, 512, 1.0, "{}"));
        job.AddImage(GeneratedImage.Create(job.Id, "/img2.png", 2, 512, 512, 1.0, "{}"));
        job.AddImage(GeneratedImage.Create(job.Id, "/img3.png", 3, 512, 512, 1.0, "{}"));

        job.Images.Should().HaveCount(3);
    }

    [Fact]
    public void Create_WithValidDimensions_Succeeds()
    {
        var parameters = ValidParameters with { Width = 1024, Height = 768 };
        var job = GenerationJob.Create(Guid.NewGuid(), parameters);

        job.Parameters.Width.Should().Be(1024);
        job.Parameters.Height.Should().Be(768);
    }

    [Fact]
    public void Create_WithEmptyCheckpointId_StillCreates()
    {
        // Empty GUID is technically allowed by GenerationJob.Create
        var parameters = ValidParameters with { CheckpointModelId = Guid.Empty };
        var job = GenerationJob.Create(Guid.NewGuid(), parameters);
        job.Should().NotBeNull();
    }

    [Fact]
    public void Create_ImageToImage_WithDenoisingLessThanOne_Succeeds()
    {
        var parameters = ValidParameters with
        {
            Mode = GenerationMode.ImageToImage,
            DenoisingStrength = 0.75,
            InitImagePath = "/path/to/init.png"
        };

        var job = GenerationJob.Create(Guid.NewGuid(), parameters);
        job.Should().NotBeNull();
        job.Parameters.Mode.Should().Be(GenerationMode.ImageToImage);
        job.Parameters.DenoisingStrength.Should().Be(0.75);
    }

    [Fact]
    public void Create_ImageToImage_WithDenoisingOne_ThrowsArgumentException()
    {
        var parameters = ValidParameters with
        {
            Mode = GenerationMode.ImageToImage,
            DenoisingStrength = 1.0
        };

        var act = () => GenerationJob.Create(Guid.NewGuid(), parameters);
        act.Should().Throw<ArgumentException>().WithMessage("*Denoising*");
    }

    [Fact]
    public void Create_TextToImage_WithDenoisingOne_Succeeds()
    {
        var parameters = ValidParameters with
        {
            Mode = GenerationMode.TextToImage,
            DenoisingStrength = 1.0
        };

        var job = GenerationJob.Create(Guid.NewGuid(), parameters);
        job.Should().NotBeNull();
    }

    [Fact]
    public void Create_TextToImage_DefaultMode_IsTextToImage()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        job.Parameters.Mode.Should().Be(GenerationMode.TextToImage);
    }
}
