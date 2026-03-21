using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record GeneratedImageDto(
    Guid Id,
    Guid GenerationJobId,
    string FilePath,
    long Seed,
    int Width,
    int Height,
    double GenerationTimeSeconds,
    string ParametersJson,
    DateTimeOffset CreatedAt,
    bool IsFavorite = false,
    ContentRating ContentRating = ContentRating.Unknown,
    double NsfwScore = 0,
    bool IsRevealed = false);
