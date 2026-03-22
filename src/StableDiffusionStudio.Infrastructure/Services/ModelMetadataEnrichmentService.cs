using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Background service that continuously enriches local model records
/// with metadata from remote providers (CivitAI, HuggingFace).
/// Runs on startup after a 15-second delay, then processes models in batches
/// with configurable delays and idle intervals.
/// </summary>
public class ModelMetadataEnrichmentService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModelMetadataEnrichmentService> _logger;

    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

    public ModelMetadataEnrichmentService(
        IServiceScopeFactory scopeFactory,
        ILogger<ModelMetadataEnrichmentService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        _logger.LogInformation("Model metadata enrichment service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            ModelEnrichmentSettings settings;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

                settings = await settingsProvider.GetAsync<ModelEnrichmentSettings>("ModelEnrichmentSettings", stoppingToken)
                           ?? ModelEnrichmentSettings.Default;

                if (!settings.Enabled)
                {
                    _logger.LogDebug("Model enrichment is disabled, sleeping for {Seconds}s", settings.IdleIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(settings.IdleIntervalSeconds), stoppingToken);
                    continue;
                }

                var enrichedInCycle = await RunEnrichmentCycleAsync(scope.ServiceProvider, settings, stoppingToken);

                if (enrichedInCycle == 0)
                {
                    _logger.LogDebug("No models needed enrichment, sleeping for {Seconds}s", settings.IdleIntervalSeconds);
                }
                else
                {
                    _logger.LogInformation("Enrichment cycle complete: {Count} models enriched", enrichedInCycle);
                }

                await Task.Delay(TimeSpan.FromSeconds(settings.IdleIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Model metadata enrichment cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }

        _logger.LogInformation("Model metadata enrichment service stopped");
    }

    private async Task<int> RunEnrichmentCycleAsync(
        IServiceProvider serviceProvider,
        ModelEnrichmentSettings settings,
        CancellationToken stoppingToken)
    {
        var repository = serviceProvider.GetRequiredService<IModelCatalogRepository>();
        var notifier = serviceProvider.GetRequiredService<IModelEnrichmentNotifier>();
        var providers = serviceProvider.GetServices<IModelEnrichmentProvider>()
            .Where(p => IsProviderEnabled(p, settings))
            .ToList();

        if (providers.Count == 0)
        {
            _logger.LogDebug("No enrichment providers are enabled");
            return 0;
        }

        var allModels = await repository.ListAsync(new ModelFilter(Take: int.MaxValue), stoppingToken);
        var cutoff = settings.RescanIntervalHours > 0
            ? DateTimeOffset.UtcNow.AddHours(-settings.RescanIntervalHours)
            : (DateTimeOffset?)null;

        var modelsNeedingEnrichment = allModels
            .Where(m => m.LastEnrichedAt is null
                        || (cutoff.HasValue && m.LastEnrichedAt < cutoff.Value))
            .ToList();

        if (modelsNeedingEnrichment.Count == 0)
            return 0;

        var totalRemaining = modelsNeedingEnrichment.Count;
        var totalEnriched = 0;

        var batches = modelsNeedingEnrichment
            .Select((model, index) => new { model, index })
            .GroupBy(x => x.index / settings.BatchSize)
            .Select(g => g.Select(x => x.model).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            stoppingToken.ThrowIfCancellationRequested();

            foreach (var model in batch)
            {
                stoppingToken.ThrowIfCancellationRequested();

                try
                {
                    var enriched = await EnrichSingleModelAsync(model, providers, stoppingToken);

                    if (enriched)
                    {
                        await repository.UpsertAsync(model, stoppingToken);
                        totalEnriched++;
                        totalRemaining--;

                        await notifier.SendModelEnrichedAsync(
                            model.Id.ToString(),
                            model.PreviewImagePath,
                            model.CivitAIUrl,
                            model.HuggingFaceUrl);

                        _logger.LogDebug("Enriched model {ModelId} ({Title})", model.Id, model.Title);
                    }
                    else
                    {
                        // Mark as enriched even if no data found, to avoid re-checking immediately
                        model.UpdateEnrichment();
                        await repository.UpsertAsync(model, stoppingToken);
                        totalRemaining--;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enrich model {ModelId} ({Title})", model.Id, model.Title);
                }

                if (settings.DelayBetweenModelsMs > 0)
                {
                    await Task.Delay(settings.DelayBetweenModelsMs, stoppingToken);
                }
            }

            await notifier.SendEnrichmentProgressAsync(
                totalEnriched,
                totalRemaining,
                isComplete: totalRemaining == 0);
        }

        return totalEnriched;
    }

    private async Task<bool> EnrichSingleModelAsync(
        Domain.Entities.ModelRecord model,
        List<IModelEnrichmentProvider> providers,
        CancellationToken ct)
    {
        var anyEnriched = false;

        foreach (var provider in providers)
        {
            try
            {
                var result = await provider.EnrichAsync(model, ct);
                if (result is null)
                    continue;

                model.UpdateMetadata(
                    title: result.Title,
                    description: result.Description,
                    tags: result.Tags,
                    previewImagePath: result.PreviewImagePath);

                var civitAIModelId = result.ProviderId == "civitai" ? result.RemoteModelId : null;
                var civitAIUrl = result.ProviderId == "civitai" ? result.ProviderUrl : null;
                var huggingFaceModelId = result.ProviderId == "huggingface" ? result.RemoteModelId : null;
                var huggingFaceUrl = result.ProviderId == "huggingface" ? result.ProviderUrl : null;

                model.UpdateEnrichment(
                    civitAIModelId: civitAIModelId,
                    civitAIUrl: civitAIUrl,
                    huggingFaceModelId: huggingFaceModelId,
                    huggingFaceUrl: huggingFaceUrl);

                anyEnriched = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {ProviderId} failed for model {ModelId}",
                    provider.ProviderId, model.Id);
            }
        }

        return anyEnriched;
    }

    private static bool IsProviderEnabled(IModelEnrichmentProvider provider, ModelEnrichmentSettings settings)
    {
        return provider.ProviderId switch
        {
            "civitai" => settings.CivitAIEnabled,
            "huggingface" => settings.HuggingFaceEnabled,
            _ => true
        };
    }
}
