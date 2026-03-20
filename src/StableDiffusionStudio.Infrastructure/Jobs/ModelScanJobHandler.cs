using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class ModelScanJobHandler : IJobHandler
{
    private readonly IModelCatalogService _catalogService;
    private readonly ILogger<ModelScanJobHandler> _logger;

    public ModelScanJobHandler(IModelCatalogService catalogService, ILogger<ModelScanJobHandler> logger)
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        _logger.LogInformation("Starting model scan job {JobId}", job.Id);

        job.UpdateProgress(10, "Scanning directories");
        var result = await _catalogService.ScanAsync(new ScanModelsCommand(job.Data), ct);

        job.UpdateProgress(100, "Scan complete");
        job.Complete($"New: {result.NewCount}, Updated: {result.UpdatedCount}, Missing: {result.MissingCount}");

        _logger.LogInformation("Model scan job {JobId} completed: {Result}", job.Id, job.ResultData);
    }
}
