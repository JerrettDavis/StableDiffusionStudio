using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record DiscoveredModel(
    string FilePath, string? Title, ModelType Type, ModelFamily Family,
    ModelFormat Format, long FileSize, string? PreviewImagePath,
    string? Description, IReadOnlyList<string> Tags);
