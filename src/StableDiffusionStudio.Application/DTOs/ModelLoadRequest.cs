namespace StableDiffusionStudio.Application.DTOs;

public sealed record ModelLoadRequest(
    string CheckpointPath,
    string? VaePath,
    IReadOnlyList<LoraLoadInfo> Loras);
