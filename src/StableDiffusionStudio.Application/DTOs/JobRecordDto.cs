using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record JobRecordDto(
    Guid Id, string Type, string? Data, JobStatus Status, int Progress, string? Phase,
    Guid CorrelationId, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt, string? ErrorMessage, string? ResultData);
