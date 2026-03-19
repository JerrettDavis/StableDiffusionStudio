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
    int BatchSize);
