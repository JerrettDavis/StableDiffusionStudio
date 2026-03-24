using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelRecordDto(
    Guid Id, string Title, ModelType Type, ModelFamily ModelFamily, ModelFormat Format, string FilePath,
    long FileSize, string Source, IReadOnlyList<string> Tags, string? Description,
    string? PreviewImagePath, string? CompatibilityHints, ModelStatus Status, DateTimeOffset DetectedAt,
    bool IsNsfw);
