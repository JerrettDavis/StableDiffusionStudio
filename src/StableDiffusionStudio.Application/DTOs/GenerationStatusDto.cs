using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record GenerationStatusDto(
    GenerationJobStatus Status,
    int Progress,
    string? Phase,
    string? ErrorMessage,
    int ImageCount,
    double? ElapsedSeconds);
