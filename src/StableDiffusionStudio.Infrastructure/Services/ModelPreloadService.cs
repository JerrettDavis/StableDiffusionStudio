using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// On startup, asynchronously loads the most recently used model in the background
/// so the first generation doesn't have to wait for model loading.
/// Only runs if PreloadLastModel is enabled in inference settings.
/// </summary>
public class ModelPreloadService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInferenceBackend _inferenceBackend;
    private readonly ILogger<ModelPreloadService> _logger;

    public ModelPreloadService(
        IServiceScopeFactory scopeFactory,
        IInferenceBackend inferenceBackend,
        ILogger<ModelPreloadService> logger)
    {
        _scopeFactory = scopeFactory;
        _inferenceBackend = inferenceBackend;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a few seconds for the app to finish starting up
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsProvider = scope.ServiceProvider.GetRequiredService<IInferenceSettingsProvider>();
            var settings = await settingsProvider.GetSettingsAsync(stoppingToken);

            if (!settings.PreloadLastModel)
            {
                _logger.LogDebug("Model preloading is disabled");
                return;
            }

            // Get the most recently used model ID
            var kvSettings = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();
            var recentModels = await kvSettings.GetAsync<List<Guid>>("recent-models", stoppingToken);
            if (recentModels is null || recentModels.Count == 0)
            {
                _logger.LogDebug("No recent models found — skipping preload");
                return;
            }

            var lastModelId = recentModels[0];
            var modelRepo = scope.ServiceProvider.GetRequiredService<IModelCatalogRepository>();
            var model = await modelRepo.GetByIdAsync(lastModelId, stoppingToken);
            if (model is null)
            {
                _logger.LogDebug("Last used model {ModelId} not found in catalog — skipping preload", lastModelId);
                return;
            }

            _logger.LogInformation("Preloading last used model: {ModelName} ({Path})",
                model.Title, model.FilePath);

            // Resolve Flux components if needed
            string? vaePath = null;
            string? clipLPath = null;
            string? t5xxlPath = null;
            var fileName = Path.GetFileName(model.FilePath).ToLowerInvariant();
            if (fileName.Contains("flux"))
            {
                var fluxResolver = scope.ServiceProvider.GetService<IFluxComponentResolver>();
                if (fluxResolver is not null)
                {
                    var components = await fluxResolver.ResolveAsync(model.FilePath, stoppingToken);
                    if (components is not null)
                    {
                        clipLPath = components.ClipLPath;
                        t5xxlPath = components.T5xxlPath;
                        vaePath = components.VaePath;
                    }
                }
            }

            await _inferenceBackend.LoadModelAsync(
                new Application.DTOs.ModelLoadRequest(model.FilePath, vaePath, [], clipLPath, t5xxlPath),
                stoppingToken);

            _logger.LogInformation("Model preloaded successfully: {ModelName}", model.Title);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Model preloading cancelled — app shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model preloading failed — first generation will load the model on demand");
        }
    }
}
