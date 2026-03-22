using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// A remote provider that can look up metadata for local models.
/// Implemented by CivitAI and HuggingFace enrichment providers.
/// </summary>
public interface IModelEnrichmentProvider
{
    string ProviderId { get; }
    string DisplayName { get; }

    /// <summary>
    /// Attempt to find and enrich a local model with remote metadata.
    /// Returns null if no match was found.
    /// </summary>
    Task<ModelEnrichmentResult?> EnrichAsync(ModelRecord model, CancellationToken ct = default);
}

/// <summary>Result of a single model enrichment lookup.</summary>
public record ModelEnrichmentResult
{
    public required string ProviderId { get; init; }
    public string? RemoteModelId { get; init; }
    public string? ProviderUrl { get; init; }
    public string? PreviewImagePath { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public string? Title { get; init; }
}
