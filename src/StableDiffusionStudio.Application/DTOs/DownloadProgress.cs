namespace StableDiffusionStudio.Application.DTOs;

public record DownloadProgress(long BytesDownloaded, long TotalBytes, string Phase);
