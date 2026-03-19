namespace StableDiffusionStudio.Application.DTOs;

public record DownloadResult(bool Success, string? LocalFilePath, string? Error);
