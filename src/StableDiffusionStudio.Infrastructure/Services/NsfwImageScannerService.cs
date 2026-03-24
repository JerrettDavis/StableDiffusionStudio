using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Background service that processes the NSFW scan queue.
/// On startup, sweeps all model preview images and enqueues any that lack NSFW metadata.
/// Then continuously drains the queue, classifying images and writing metadata back into PNG files.
/// </summary>
public class NsfwImageScannerService : BackgroundService
{
    private readonly NsfwScanQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NsfwImageScannerService> _logger;

    public NsfwImageScannerService(
        NsfwScanQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NsfwImageScannerService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay startup to let the app finish initializing
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        // Sweep existing model preview images for unprocessed files
        await SweepExistingPreviewsAsync(stoppingToken);

        // Continuously process the queue
        _logger.LogInformation("NSFW image scanner started. Queue has {Count} items", _queue.Count);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var filePath = await _queue.DequeueAsync(stoppingToken);
                if (filePath is null) continue;

                await ProcessImageAsync(filePath, stoppingToken);

                // Throttle to avoid CPU saturation
                await Task.Delay(500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing NSFW scan queue item");
                await Task.Delay(1000, stoppingToken); // Back off on error
            }
        }
    }

    private async Task SweepExistingPreviewsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IModelCatalogRepository>();
            var models = await repo.ListAsync(new Application.DTOs.ModelFilter(Take: 10000), ct);

            var enqueued = 0;
            foreach (var model in models)
            {
                if (ct.IsCancellationRequested) break;
                if (model.PreviewImagePath is null) continue;
                if (!File.Exists(model.PreviewImagePath)) continue;

                var existing = PngMetadataService.ReadNsfwClassification(model.PreviewImagePath);
                if (existing is null)
                {
                    _queue.Enqueue(model.PreviewImagePath);
                    enqueued++;
                }
            }

            if (enqueued > 0)
                _logger.LogInformation("Enqueued {Count} model preview images for NSFW scanning", enqueued);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sweep existing model preview images");
        }
    }

    private async Task ProcessImageAsync(string filePath, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Skipping {Path} — file no longer exists", filePath);
                return;
            }

            // Only process PNGs (non-PNG files are skipped by PngMetadataService)
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is not ".png")
            {
                _logger.LogDebug("Skipping {Path} — not a PNG file", filePath);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var contentSafety = scope.ServiceProvider.GetRequiredService<IContentSafetyService>();

            var imageBytes = await File.ReadAllBytesAsync(filePath, ct);
            var result = await contentSafety.ClassifyAsync(imageBytes, ct);

            var classification = new NsfwClassification(
                result.Rating,
                result.NsfwScore,
                result.PornographyScore,
                result.SexyScore,
                result.HentaiScore,
                DateTimeOffset.UtcNow);

            PngMetadataService.WriteNsfwClassification(filePath, classification);

            // Invalidate blurred cache if it exists
            var blurredPath = filePath + ".blurred.png";
            if (File.Exists(blurredPath))
            {
                try { File.Delete(blurredPath); }
                catch { /* best effort */ }
            }

            _logger.LogDebug("Classified {Path}: {Rating} (score: {Score:F3})",
                filePath, result.Rating, result.NsfwScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to classify image {Path}", filePath);
        }
    }
}
