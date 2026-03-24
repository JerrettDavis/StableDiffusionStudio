using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelCardViewModel(
    string Id, string Title, string? PreviewImageUrl,
    ModelType Type, ModelFamily Family, ModelFormat Format,
    long? FileSize, string Source, bool IsLocal, bool IsAvailable,
    string? Description, bool IsNsfw)
{
    public static ModelCardViewModel FromLocal(ModelRecordDto dto, IAppPaths appPaths)
    {
        // Use the model-preview API endpoint for preview images stored in model directories
        // (outside the app's Assets directory). This handles arbitrary disk paths securely.
        string? previewUrl = null;
        if (dto.PreviewImagePath is not null)
        {
            var assetUrl = appPaths.GetImageUrl(dto.PreviewImagePath);
            // If GetImageUrl returned a relative /assets/ URL, use it directly.
            // Otherwise it returned the raw path — use the model-preview endpoint instead.
            previewUrl = assetUrl.StartsWith("/assets/")
                ? assetUrl
                : $"/api/model-preview/{dto.Id}";
        }

        return new ModelCardViewModel(
            Id: dto.Id.ToString(),
            Title: dto.Title,
            PreviewImageUrl: previewUrl,
            Type: dto.Type,
            Family: dto.ModelFamily,
            Format: dto.Format,
            FileSize: dto.FileSize,
            Source: "local",
            IsLocal: true,
            IsAvailable: dto.Status == ModelStatus.Available,
            Description: dto.Description,
            IsNsfw: dto.IsNsfw);
    }

    public static ModelCardViewModel FromRemote(RemoteModelInfo info, string providerId)
    {
        return new ModelCardViewModel(
            Id: info.ExternalId,
            Title: info.Title,
            PreviewImageUrl: info.PreviewImageUrl,
            Type: info.Type,
            Family: info.Family,
            Format: info.Format,
            FileSize: info.FileSize,
            Source: providerId,
            IsLocal: false,
            IsAvailable: true,
            Description: info.Description,
            IsNsfw: false);
    }
}
