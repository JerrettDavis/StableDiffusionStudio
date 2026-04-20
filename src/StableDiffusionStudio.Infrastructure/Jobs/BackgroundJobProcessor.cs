using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class BackgroundJobProcessor : BackgroundService
{
    private readonly JobChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobProcessor> _logger;

    public BackgroundJobProcessor(JobChannel channel, IServiceScopeFactory scopeFactory,
        ILogger<BackgroundJobProcessor> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background job processor started.");

        await foreach (var jobId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(jobId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error processing job {JobId}", jobId);
            }
        }
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await context.JobRecords.FindAsync([jobId], ct);
        if (job is null)
        {
            _logger.LogWarning("Job {JobId} not found.", jobId);
            return;
        }

        var handler = scope.ServiceProvider.GetKeyedService<IJobHandler>(job.Type);
        if (handler is null)
        {
            _logger.LogError("No handler registered for job type '{Type}'.", job.Type);
            job.Fail($"No handler registered for job type '{job.Type}'.");
            await context.SaveChangesAsync(ct);
            return;
        }

        try
        {
            job.Start();
            await context.SaveChangesAsync(ct);

            await handler.HandleAsync(job, ct);

            if (job.Status == JobStatus.Running)
                job.Complete();

            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Job {JobId} ({Type}) completed.", jobId, job.Type);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Job {JobId} ({Type}) failed.", jobId, job.Type);
            job.Fail(ex.Message);
            await context.SaveChangesAsync(ct);
        }
    }
}
