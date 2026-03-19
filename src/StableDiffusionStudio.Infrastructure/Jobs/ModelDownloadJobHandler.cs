using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class ModelDownloadJobHandler : IJobHandler
{
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly IModelCatalogRepository _catalogRepository;
    private readonly ILogger<ModelDownloadJobHandler> _logger;

    public ModelDownloadJobHandler(
        IEnumerable<IModelProvider> providers,
        IModelCatalogRepository catalogRepository,
        ILogger<ModelDownloadJobHandler> logger)
    {
        _providers = providers;
        _catalogRepository = catalogRepository;
        _logger = logger;
    }

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<DownloadRequest>(job.Data!);
        if (request is null)
        {
            job.Fail("Invalid download request data");
            return;
        }

        var provider = _providers.FirstOrDefault(p => p.ProviderId == request.ProviderId);
        if (provider is null)
        {
            job.Fail($"Unknown provider: {request.ProviderId}");
            return;
        }

        job.UpdateProgress(5, "Starting download");
        _logger.LogInformation("Downloading {ExternalId} from {Provider}", request.ExternalId, request.ProviderId);

        var progress = new Progress<DownloadProgress>(p =>
        {
            var pct = p.TotalBytes > 0 ? (int)(p.BytesDownloaded * 85 / p.TotalBytes) + 5 : 50;
            job.UpdateProgress(pct, $"{p.Phase} ({p.BytesDownloaded / 1_000_000}MB / {p.TotalBytes / 1_000_000}MB)");
        });

        var result = await provider.DownloadAsync(request, progress, ct);

        if (result.Success && result.LocalFilePath is not null)
        {
            job.UpdateProgress(95, "Registering in catalog");
            var fileInfo = new FileInfo(result.LocalFilePath);
            var modelFileInfo = new ModelFileInfo(fileInfo.Name, fileInfo.Length, null);
            var format = ModelFileAnalyzer.InferFormat(modelFileInfo);
            var family = ModelFileAnalyzer.InferFamily(modelFileInfo);
            var modelType = request.Type != ModelType.Unknown ? request.Type : ModelFileAnalyzer.InferModelType(modelFileInfo);

            var record = ModelRecord.Create(null, result.LocalFilePath, family, format, fileInfo.Length,
                request.ProviderId, modelType);
            await _catalogRepository.UpsertAsync(record, ct);

            job.Complete($"Downloaded to {Path.GetFileName(result.LocalFilePath)}");
            _logger.LogInformation("Download complete: {FilePath}", result.LocalFilePath);
        }
        else
        {
            job.Fail(result.Error ?? "Download failed");
            _logger.LogWarning("Download failed for {ExternalId}: {Error}", request.ExternalId, result.Error);
        }
    }
}
