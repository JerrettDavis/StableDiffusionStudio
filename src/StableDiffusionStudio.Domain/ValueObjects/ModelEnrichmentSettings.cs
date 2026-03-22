namespace StableDiffusionStudio.Domain.ValueObjects;

/// <summary>
/// Configuration for the model metadata enrichment system.
/// Stored via ISettingsProvider with key "ModelEnrichmentSettings".
/// </summary>
public sealed record ModelEnrichmentSettings
{
    /// <summary>Whether enrichment is enabled at all.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Use CivitAI for enrichment (requires API key in Model Sources settings).</summary>
    public bool CivitAIEnabled { get; init; } = true;

    /// <summary>Use HuggingFace for enrichment (works without key, better with token).</summary>
    public bool HuggingFaceEnabled { get; init; } = true;

    /// <summary>Batch size per enrichment cycle.</summary>
    public int BatchSize { get; init; } = 10;

    /// <summary>Delay between individual model lookups (milliseconds) to rate-limit API calls.</summary>
    public int DelayBetweenModelsMs { get; init; } = 1500;

    /// <summary>
    /// How often to re-check already-enriched models for updated metadata (hours).
    /// 0 = never re-check. Default = 168 (weekly).
    /// </summary>
    public int RescanIntervalHours { get; init; } = 168;

    /// <summary>
    /// How often the background worker checks for unenriched models (seconds).
    /// When all models are enriched, this becomes the idle polling interval.
    /// </summary>
    public int IdleIntervalSeconds { get; init; } = 300;

    public static ModelEnrichmentSettings Default => new();
}
