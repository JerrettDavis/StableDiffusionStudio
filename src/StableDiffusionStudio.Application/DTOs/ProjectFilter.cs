using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ProjectFilter(
    string? SearchTerm = null,
    ProjectStatus? Status = null,
    bool? IsPinned = null,
    int Skip = 0,
    int Take = 50);
