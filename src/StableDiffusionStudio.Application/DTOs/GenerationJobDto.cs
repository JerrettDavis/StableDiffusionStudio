using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.DTOs;

public record GenerationJobDto(
    Guid Id,
    Guid ProjectId,
    GenerationParameters Parameters,
    GenerationJobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage,
    IReadOnlyList<GeneratedImageDto> Images);
