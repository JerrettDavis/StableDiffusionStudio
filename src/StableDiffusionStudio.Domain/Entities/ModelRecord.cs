using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class ModelRecord
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ModelType Type { get; private set; }
    public ModelFamily ModelFamily { get; private set; }
    public ModelFormat Format { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public string? Checksum { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public IReadOnlyList<string> Tags { get; private set; } = Array.Empty<string>();
    public string? Description { get; private set; }
    public string? PreviewImagePath { get; private set; }
    public string? CompatibilityHints { get; private set; }
    public DateTimeOffset DetectedAt { get; private set; }
    public DateTimeOffset? LastVerifiedAt { get; private set; }
    public ModelStatus Status { get; private set; }

    private ModelRecord() { } // EF Core

    public static ModelRecord Create(string? title, string filePath, ModelFamily modelFamily,
        ModelFormat format, long fileSize, string source, ModelType type = ModelType.Checkpoint)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        return new ModelRecord
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileName(filePath) : title,
            Type = type,
            ModelFamily = modelFamily,
            Format = format,
            FilePath = filePath,
            FileSize = fileSize,
            Source = source,
            DetectedAt = DateTimeOffset.UtcNow,
            LastVerifiedAt = DateTimeOffset.UtcNow,
            Status = ModelStatus.Available
        };
    }

    public void MarkMissing()
    {
        Status = ModelStatus.Missing;
    }

    public void MarkAvailable()
    {
        Status = ModelStatus.Available;
        LastVerifiedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateMetadata(string? title = null, ModelFamily? modelFamily = null, string? description = null,
        IReadOnlyList<string>? tags = null, string? previewImagePath = null, string? compatibilityHints = null,
        ModelType? type = null)
    {
        if (title is not null) Title = title;
        if (modelFamily.HasValue) ModelFamily = modelFamily.Value;
        if (description is not null) Description = description;
        if (tags is not null) Tags = tags;
        if (previewImagePath is not null) PreviewImagePath = previewImagePath;
        if (compatibilityHints is not null) CompatibilityHints = compatibilityHints;
        if (type.HasValue) Type = type.Value;
        LastVerifiedAt = DateTimeOffset.UtcNow;
    }
}
