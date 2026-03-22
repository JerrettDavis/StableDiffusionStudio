namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Service interface for manually triggering model metadata enrichment.
/// </summary>
public interface IModelEnrichmentService
{
    /// <summary>
    /// Enrich all models that need enrichment. Returns the number of models enriched.
    /// </summary>
    Task<int> EnrichAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Enrich a single model by ID. Returns true if enrichment was applied.
    /// </summary>
    Task<bool> EnrichModelAsync(Guid modelId, CancellationToken ct = default);
}
