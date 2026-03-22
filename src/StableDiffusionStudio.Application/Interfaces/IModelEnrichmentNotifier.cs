namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Sends real-time notifications when model metadata is enriched,
/// so the UI can update without polling.
/// </summary>
public interface IModelEnrichmentNotifier
{
    /// <summary>A model's metadata was updated (preview, description, tags, cross-links).</summary>
    Task SendModelEnrichedAsync(string modelId, string? previewImageUrl, string? civitAIUrl, string? huggingFaceUrl);

    /// <summary>An enrichment cycle completed (for progress tracking).</summary>
    Task SendEnrichmentProgressAsync(int enrichedCount, int remainingCount, bool isComplete);
}
