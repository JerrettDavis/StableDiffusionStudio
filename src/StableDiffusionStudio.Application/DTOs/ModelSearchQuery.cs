using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelSearchQuery(
    string ProviderId, string? SearchTerm = null, ModelType? Type = null,
    ModelFamily? Family = null, string? Tag = null,
    SortOrder Sort = SortOrder.Relevance, int Page = 0, int PageSize = 20);
