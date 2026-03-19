using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    ProjectStatus Status,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
