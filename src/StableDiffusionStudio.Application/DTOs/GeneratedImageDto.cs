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
    bool IsFavorite = false);
