using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public sealed record InferenceRequest(
    string PositivePrompt,
    string NegativePrompt,
    Sampler Sampler,
    Scheduler Scheduler,
    int Steps,
    double CfgScale,
    long Seed,
    int Width,
    int Height,
    int BatchSize,
    int ClipSkip = 1,
    double Eta = 0.0,
    byte[]? InitImage = null,
    double DenoisingStrength = 1.0,
    byte[]? MaskImage = null,
    ImageInputMode ImageInputMode = ImageInputMode.Scale);
