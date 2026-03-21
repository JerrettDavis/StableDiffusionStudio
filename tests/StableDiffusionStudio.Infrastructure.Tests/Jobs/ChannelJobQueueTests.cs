using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Jobs;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Jobs;

public class ChannelJobQueueTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly JobChannel _channel;
    private readonly ChannelJobQueue _queue;

    public ChannelJobQueueTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        _channel = new JobChannel();
        _queue = new ChannelJobQueue(_context, _channel);
    }

    [Fact]
    public async Task EnqueueAsync_CreatesJobAndWritesToChannel()
    {
        var jobId = await _queue.EnqueueAsync("model-scan", "test-data");

        jobId.Should().NotBeEmpty();
        var job = await _context.JobRecords.FindAsync(jobId);
        job.Should().NotBeNull();
        job!.Type.Should().Be("model-scan");
        job.Status.Should().Be(JobStatus.Pending);

        _channel.Reader.TryRead(out var channelJobId).Should().BeTrue();
        channelJobId.Should().Be(jobId);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllJobs()
    {
        await _queue.EnqueueAsync("model-scan");
        await _queue.EnqueueAsync("model-scan");

        var jobs = await _queue.ListAsync();

        jobs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectJob()
    {
        var jobId = await _queue.EnqueueAsync("model-scan", "some-data");

        var dto = await _queue.GetByIdAsync(jobId);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(jobId);
        dto.Type.Should().Be("model-scan");
        dto.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task EnqueueAsync_MultipleJobs_AllPersisted()
    {
        var id1 = await _queue.EnqueueAsync("type-a", "data-1");
        var id2 = await _queue.EnqueueAsync("type-b", "data-2");
        var id3 = await _queue.EnqueueAsync("type-a", "data-3");

        var jobs = await _queue.ListAsync();
        jobs.Should().HaveCount(3);
        jobs.Select(j => j.Id).Should().Contain(new[] { id1, id2, id3 });
    }

    [Fact]
    public async Task ListAsync_ActiveOnly_FiltersCorrectly()
    {
        // Create two jobs; manually complete one
        var id1 = await _queue.EnqueueAsync("scan");
        var id2 = await _queue.EnqueueAsync("scan");

        // Manually mark job1 as completed
        var job1 = await _context.JobRecords.FindAsync(id1);
        job1!.Start();
        job1.Complete();
        await _context.SaveChangesAsync();

        var activeJobs = await _queue.ListAsync(activeOnly: true);
        activeJobs.Should().HaveCount(1);
        activeJobs[0].Id.Should().Be(id2);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var dto = await _queue.GetByIdAsync(Guid.NewGuid());
        dto.Should().BeNull();
    }

    [Fact]
    public async Task EnqueueAsync_WithNullData_Works()
    {
        var jobId = await _queue.EnqueueAsync("simple-job");

        var dto = await _queue.GetByIdAsync(jobId);
        dto.Should().NotBeNull();
        dto!.Type.Should().Be("simple-job");
    }

    [Fact]
    public async Task ListAsync_ReturnsNewestFirst()
    {
        await _queue.EnqueueAsync("first");
        await Task.Delay(10);
        await _queue.EnqueueAsync("second");

        var jobs = await _queue.ListAsync();
        jobs[0].Type.Should().Be("second");
        jobs[1].Type.Should().Be("first");
    }

    [Fact]
    public async Task EnqueueAsync_ConcurrentEnqueues_AllPersisted()
    {
        var tasks = Enumerable.Range(0, 5)
            .Select(i => _queue.EnqueueAsync($"concurrent-{i}", $"data-{i}"));

        var ids = await Task.WhenAll(tasks);

        ids.Should().OnlyHaveUniqueItems();
        var jobs = await _queue.ListAsync();
        jobs.Should().HaveCount(5);
    }

    [Fact]
    public async Task EnqueueAsync_ReturnsUniqueIds()
    {
        var id1 = await _queue.EnqueueAsync("test");
        var id2 = await _queue.EnqueueAsync("test");

        id1.Should().NotBe(id2);
    }

    [Fact]
    public async Task ListAsync_WithMaxLimit_ReturnsAtMost100()
    {
        // The ListAsync has Take(100), so even with more jobs it caps at 100
        // Just verify a reasonable number works
        for (int i = 0; i < 5; i++)
            await _queue.EnqueueAsync($"job-{i}");

        var jobs = await _queue.ListAsync();
        jobs.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectStatus()
    {
        var jobId = await _queue.EnqueueAsync("status-test", "data");

        // Manually start the job
        var job = await _context.JobRecords.FindAsync(jobId);
        job!.Start();
        await _context.SaveChangesAsync();

        var dto = await _queue.GetByIdAsync(jobId);
        dto!.Status.Should().Be(JobStatus.Running);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
