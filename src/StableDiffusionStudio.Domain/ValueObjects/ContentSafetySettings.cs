using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record ContentSafetySettings
{
    public NsfwFilterMode FilterMode { get; init; } = NsfwFilterMode.Off;
    public double NsfwThreshold { get; init; } = 0.5;        // Score above this = NSFW
    public double QuestionableThreshold { get; init; } = 0.3;  // Score above this = Questionable
    public bool ScanExistingOnEnable { get; init; } = false;   // Re-scan existing images when enabling

    public static ContentSafetySettings Default => new();
}
