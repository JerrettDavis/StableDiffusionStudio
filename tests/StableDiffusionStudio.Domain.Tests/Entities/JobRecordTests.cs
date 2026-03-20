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

    [Fact]
    public void UpdateProgress_ClampsToZero()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();

        job.UpdateProgress(-10, "Underflow");

        job.Progress.Should().Be(0);
    }

    [Fact]
    public void UpdateProgress_ClampsTo100()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();

        job.UpdateProgress(150, "Overflow");

        job.Progress.Should().Be(100);
    }

    [Fact]
    public void Complete_SetsProgressTo100()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();
        job.UpdateProgress(50, "Halfway");

        job.Complete("Done");

        job.Progress.Should().Be(100);
    }

    [Fact]
    public void Fail_PreservesExistingProgress()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();
        job.UpdateProgress(75, "Almost there");

        job.Fail("Error occurred");

        job.Progress.Should().Be(75);
    }

    [Fact]
    public void Start_WhenNotPending_ThrowsInvalidOperationException()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();

        var act = () => job.Start();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_WhenNotRunning_ThrowsInvalidOperationException()
    {
        var job = JobRecord.Create("model-scan");

        var act = () => job.Complete();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsInvalidOperationException()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();
        job.Complete();

        var act = () => job.Cancel();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_WhenCompleted_ThrowsInvalidOperationException()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();
        job.Complete();

        var act = () => job.Fail("error");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateProgress_WhenNotRunning_ThrowsInvalidOperationException()
    {
        var job = JobRecord.Create("model-scan");

        var act = () => job.UpdateProgress(50);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_WithEmptyType_ThrowsArgumentException()
    {
        var act = () => JobRecord.Create("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_WhenRunning_TransitionsToCancelled()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();

        job.Cancel();

        job.Status.Should().Be(JobStatus.Cancelled);
        job.CompletedAt.Should().NotBeNull();
    }
}
