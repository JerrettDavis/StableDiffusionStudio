using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IContentSafetyService
{
    Task<ContentClassification> ClassifyAsync(byte[] imageBytes, CancellationToken ct = default);
    Task<NsfwFilterMode> GetFilterModeAsync(CancellationToken ct = default);
    Task SetFilterModeAsync(NsfwFilterMode mode, CancellationToken ct = default);
}

public record ContentClassification(
    ContentRating Rating,
    double NsfwScore,       // 0.0-1.0 combined NSFW probability
    double PornographyScore,
    double SexyScore,
    double HentaiScore,
    double NeutralScore);
