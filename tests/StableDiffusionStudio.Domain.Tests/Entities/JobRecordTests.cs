using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class JobRecordTests
{
    [Fact]
    public void Create_WithValidType_SetsPropertiesCorrectly()
    {
        var job = JobRecord.Create("model-scan", "some-data");

        job.Id.Should().NotBeEmpty();
        job.Type.Should().Be("model-scan");
        job.Data.Should().Be("some-data");
        job.Status.Should().Be(JobStatus.Pending);
        job.Progress.Should().Be(0);
        job.CorrelationId.Should().NotBeEmpty();
        job.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        job.StartedAt.Should().BeNull();
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Start_WhenPending_TransitionsToRunning()
    {
        var job = JobRecord.Create("model-scan");

        job.Start();

        job.Status.Should().Be(JobStatus.Running);
        job.StartedAt.Should().NotBeNull();
        job.StartedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateProgress_WhenRunning_UpdatesProgressAndPhase()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();

        job.UpdateProgress(50, "Scanning directories");

        job.Progress.Should().Be(50);
        job.Phase.Should().Be("Scanning directories");
    }

    [Fact]
    public void Complete_WhenRunning_TransitionsToCompleted()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();

        job.Complete("Result data");

        job.Status.Should().Be(JobStatus.Completed);
        job.Progress.Should().Be(100);
        job.CompletedAt.Should().NotBeNull();
        job.ResultData.Should().Be("Result data");
    }

    [Fact]
    public void Fail_WhenRunning_TransitionsToFailed()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();

        job.Fail("Something went wrong");

        job.Status.Should().Be(JobStatus.Failed);
        job.CompletedAt.Should().NotBeNull();
        job.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void Cancel_WhenPending_TransitionsToCancelled()
    {
        var job = JobRecord.Create("model-scan");

        job.Cancel();

        job.Status.Should().Be(JobStatus.Cancelled);
        job.CompletedAt.Should().NotBeNull();
    }
}
