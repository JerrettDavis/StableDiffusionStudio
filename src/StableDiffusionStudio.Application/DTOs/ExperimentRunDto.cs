using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ExperimentRunDto(
    Guid Id, Guid ExperimentId,
    long FixedSeed, bool UseFixedSeed,
    int TotalCombinations, int CompletedCount,
    ExperimentRunStatus Status, string? ErrorMessage,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    IReadOnlyList<ExperimentRunImageDto> Images);
