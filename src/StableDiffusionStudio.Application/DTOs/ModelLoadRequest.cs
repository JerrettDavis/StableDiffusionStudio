namespace StableDiffusionStudio.Application.DTOs;

public sealed record ModelLoadRequest(
    string CheckpointPath,
    string? VaePath,
    IReadOnlyList<LoraLoadInfo> Loras,
    string? ClipLPath = null,
    string? T5xxlPath = null);
