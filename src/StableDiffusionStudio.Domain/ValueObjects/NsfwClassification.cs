using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record NsfwClassification(
    ContentRating Rating, double Score,
    double PornScore, double SexyScore, double HentaiScore,
    DateTimeOffset ScannedAt, int Version = 1);
