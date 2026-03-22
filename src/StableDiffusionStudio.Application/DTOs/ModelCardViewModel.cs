using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelCardViewModel(
    string Id, string Title, string? PreviewImageUrl,
    ModelType Type, ModelFamily Family, ModelFormat Format,
    long? FileSize, string Source, bool IsLocal, bool IsAvailable,
    string? Description)
{
    public static ModelCardViewModel FromLocal(ModelRecordDto dto, IAppPaths appPaths)
    {
        var previewUrl = dto.PreviewImagePath is not null
            ? appPaths.GetImageUrl(dto.PreviewImagePath)
            : null;

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
            Description: dto.Description);
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
            Description: info.Description);
    }
}
