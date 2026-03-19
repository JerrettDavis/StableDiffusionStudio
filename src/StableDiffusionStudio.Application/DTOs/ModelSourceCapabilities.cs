namespace StableDiffusionStudio.Application.DTOs;

public record ModelSourceCapabilities(bool CanScanLocal, bool CanDownload, bool CanSearch, bool RequiresAuth);
