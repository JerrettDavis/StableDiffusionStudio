using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ExperimentRunImageDto(
    Guid Id, Guid RunId,
    string FilePath, long Seed, double GenerationTimeSeconds,
    string AxisValuesJson, int GridX, int GridY,
    bool IsWinner,
    ContentRating ContentRating, double NsfwScore);
