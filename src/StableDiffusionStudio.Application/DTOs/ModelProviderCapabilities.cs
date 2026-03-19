using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ModelProviderCapabilities(
    bool CanScanLocal, bool CanSearch, bool CanDownload,
    bool RequiresAuth, IReadOnlyList<ModelType> SupportedModelTypes);
