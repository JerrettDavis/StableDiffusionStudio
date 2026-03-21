namespace StableDiffusionStudio.Application.DTOs;

public sealed record InferenceProgress(
    int Step,
    int TotalSteps,
    string Phase,
    byte[]? PreviewImageBytes = null);
