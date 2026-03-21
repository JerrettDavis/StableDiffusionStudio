using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Jobs;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Jobs;

public class BackgroundJobProcessorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly AppDbContext _context;

    public BackgroundJobProcessorTests()
    {
        // Use a shared in-memory SQLite connection that stays open
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new AppDbContext(_dbOptions);
        _context.Database.EnsureCreated();
    }

    private ServiceProvider BuildServiceProvider(Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        // Register AppDbContext so scoped instances share the same connection
        services.AddScoped(_ => new AppDbContext(_dbOptions));
        services.AddSingleton<JobChannel>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ProcessJob_ExecutesHandler_AndCompletesJob()
    {
        var handlerExecuted = false;
        await using var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedSingleton<IJobHandler>("test-job",
                new TestJobHandler(() => handlerExecuted = true));
        });

        var channel = sp.GetRequiredService<JobChannel>();
        var job = JobRecord.Create("test-job", "data");
        _context.JobRecords.Add(job);
        await _context.SaveChangesAsync();

        var processor = new BackgroundJobProcessor(
            channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<BackgroundJobProcessor>>());

        using var cts = new CancellationTokenSource();
        await processor.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(job.Id);
        await Task.Delay(300);
        cts.Cancel();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        handlerExecuted.Should().BeTrue();

        // Re-read from DB using fresh context
        await using var verifyCtx = new AppDbContext(_dbOptions);
        var completed = await verifyCtx.JobRecords.FindAsync(job.Id);
        completed!.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task ProcessJob_HandlerFails_MarksJobAsFailed()
    {
        await using var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedSingleton<IJobHandler>("failing-job",
                new TestJobHandler(() => throw new InvalidOperationException("Something broke")));
        });

        var channel = sp.GetRequiredService<JobChannel>();
        var job = JobRecord.Create("failing-job", "data");
        _context.JobRecords.Add(job);
        await _context.SaveChangesAsync();

        var processor = new BackgroundJobProcessor(
            channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<BackgroundJobProcessor>>());

        using var cts = new CancellationTokenSource();
        await processor.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(job.Id);
        await Task.Delay(300);
        cts.Cancel();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        await using var verifyCtx = new AppDbContext(_dbOptions);
        var failed = await verifyCtx.JobRecords.FindAsync(job.Id);
        failed!.Status.Should().Be(JobStatus.Failed);
        failed.ErrorMessage.Should().Contain("Something broke");
    }

    [Fact]
    public async Task ProcessJob_MissingHandler_MarksJobAsFailed()
    {
        await using var sp = BuildServiceProvider(); // No handlers registered

        var channel = sp.GetRequiredService<JobChannel>();
        var job = JobRecord.Create("unknown-type", "data");
        _context.JobRecords.Add(job);
        await _context.SaveChangesAsync();

        var processor = new BackgroundJobProcessor(
            channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<BackgroundJobProcessor>>());

        using var cts = new CancellationTokenSource();
        await processor.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(job.Id);
        await Task.Delay(300);
        cts.Cancel();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        await using var verifyCtx = new AppDbContext(_dbOptions);
        var failed = await verifyCtx.JobRecords.FindAsync(job.Id);
        failed!.Status.Should().Be(JobStatus.Failed);
        failed.ErrorMessage.Should().Contain("No handler registered");
    }

    [Fact]
    public async Task ProcessJob_JobNotFound_DoesNotThrow()
    {
        await using var sp = BuildServiceProvider();

        var channel = sp.GetRequiredService<JobChannel>();
        var processor = new BackgroundJobProcessor(
            channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<BackgroundJobProcessor>>());

        using var cts = new CancellationTokenSource();
        await processor.StartAsync(cts.Token);

        // Write a non-existent job ID - should not throw
        await channel.Writer.WriteAsync(Guid.NewGuid());
        await Task.Delay(300);

        cts.Cancel();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }
        // If we get here without exception, the test passes
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessJob_ContinuesProcessingAfterFailure()
    {
        var executionOrder = new List<string>();
        await using var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedSingleton<IJobHandler>("failing-job",
                new TestJobHandler(() =>
                {
                    executionOrder.Add("failing");
                    throw new InvalidOperationException("Boom");
                }));
            services.AddKeyedSingleton<IJobHandler>("good-job",
                new TestJobHandler(() => executionOrder.Add("good")));
        });

        var channel = sp.GetRequiredService<JobChannel>();
        var failJob = JobRecord.Create("failing-job", "data");
        var goodJob = JobRecord.Create("good-job", "data");
        _context.JobRecords.Add(failJob);
        _context.JobRecords.Add(goodJob);
        await _context.SaveChangesAsync();

        var processor = new BackgroundJobProcessor(
            channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<BackgroundJobProcessor>>());

        using var cts = new CancellationTokenSource();
        await processor.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(failJob.Id);
        await channel.Writer.WriteAsync(goodJob.Id);
        await Task.Delay(500);
        cts.Cancel();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        executionOrder.Should().Contain("failing");
        executionOrder.Should().Contain("good");
    }

    [Fact]
    public async Task ProcessJob_SetsStartedAtTimestamp()
    {
        await using var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedSingleton<IJobHandler>("timed-job",
                new TestJobHandler(() => { }));
        });

        var channel = sp.GetRequiredService<JobChannel>();
        var job = JobRecord.Create("timed-job", "data");
        _context.JobRecords.Add(job);
        await _context.SaveChangesAsync();

        var processor = new BackgroundJobProcessor(
            channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<BackgroundJobProcessor>>());

        using var cts = new CancellationTokenSource();
        await processor.StartAsync(cts.Token);
        await channel.Writer.WriteAsync(job.Id);
        await Task.Delay(300);
        cts.Cancel();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        await using var verifyCtx = new AppDbContext(_dbOptions);
        var completed = await verifyCtx.JobRecords.FindAsync(job.Id);
        completed!.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessJob_MultipleJobs_AllComplete()
    {
        var count = 0;
        await using var sp = BuildServiceProvider(services =>
        {
            services.AddKeyedSingleton<IJobHandler>("counter-job",
                new TestJobHandler(() => Interlocked.Increment(ref count)));
        });

        var channel = sp.GetRequiredService<JobChannel>();
        var jobs = new List<JobRecord>();
        for (int i = 0; i < 3; i++)
        {
            var job = JobRecord.Create("counter-job", $"data-{i}");
            _context.JobRecords.Add(job);
            jobs.Add(job);
        }
        await _context.SaveChangesAsync();

        var processor = new BackgroundJobProcessor(
            channel,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<BackgroundJobProcessor>>());

        using var cts = new CancellationTokenSource();
        await processor.StartAsync(cts.Token);
        foreach (var job in jobs)
            await channel.Writer.WriteAsync(job.Id);
        await Task.Delay(500);
        cts.Cancel();
        try { await processor.StopAsync(CancellationToken.None); } catch (OperationCanceledException) { }

        count.Should().Be(3);

        await using var verifyCtx = new AppDbContext(_dbOptions);
        foreach (var job in jobs)
        {
            var completed = await verifyCtx.JobRecords.FindAsync(job.Id);
            completed!.Status.Should().Be(JobStatus.Completed);
        }
    }

    private class TestJobHandler : IJobHandler
    {
        private readonly Action _action;
        public TestJobHandler(Action action) => _action = action;

        public Task HandleAsync(JobRecord job, CancellationToken ct)
        {
            _action();
            return Task.CompletedTask;
        }
    }
}
