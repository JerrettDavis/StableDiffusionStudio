using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record RemoteModelInfo(
    string ExternalId, string Title, string? Description,
    ModelType Type, ModelFamily Family, ModelFormat Format,
    long? FileSize, string? PreviewImageUrl, IReadOnlyList<string> Tags,
    string ProviderUrl, IReadOnlyList<ModelFileVariant> Variants);
