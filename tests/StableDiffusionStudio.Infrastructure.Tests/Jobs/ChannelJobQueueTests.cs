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

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
