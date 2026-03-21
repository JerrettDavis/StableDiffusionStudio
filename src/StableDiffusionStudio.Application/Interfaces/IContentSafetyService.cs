using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IContentSafetyService
{
    Task<ContentClassification> ClassifyAsync(byte[] imageBytes, CancellationToken ct = default);
    Task<NsfwFilterMode> GetFilterModeAsync(CancellationToken ct = default);
    Task SetFilterModeAsync(NsfwFilterMode mode, CancellationToken ct = default);
    Task<ContentSafetyThresholds> GetThresholdsAsync(CancellationToken ct = default);
    Task SetThresholdsAsync(ContentSafetyThresholds thresholds, CancellationToken ct = default);
    Task<bool> GetNsfwShieldEnabledAsync(CancellationToken ct = default);
    Task SetNsfwShieldEnabledAsync(bool enabled, CancellationToken ct = default);
}

public record ContentSafetyThresholds(double NsfwThreshold, double QuestionableThreshold);

public record ContentClassification(
    ContentRating Rating,
    double NsfwScore,       // 0.0-1.0 combined NSFW probability
    double PornographyScore,
    double SexyScore,
    double HentaiScore,
    double NeutralScore);
