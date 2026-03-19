namespace StableDiffusionStudio.Application.DTOs;

public sealed record InferenceResult(
    bool Success,
    IReadOnlyList<GeneratedImageData> Images,
    string? Error);
