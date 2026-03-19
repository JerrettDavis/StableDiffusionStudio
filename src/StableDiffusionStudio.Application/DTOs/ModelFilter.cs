using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelFilter(
    string? SearchTerm = null, ModelFamily? Family = null, ModelFormat? Format = null,
    ModelStatus? Status = null, string? Source = null, ModelType? Type = null, int Skip = 0, int Take = 50);
