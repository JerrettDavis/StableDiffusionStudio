using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public sealed record InferenceCapabilities(
    IReadOnlyList<ModelFamily> SupportedFamilies,
    IReadOnlyList<Sampler> SupportedSamplers,
    int MaxWidth,
    int MaxHeight,
    bool SupportsLoRA,
    bool SupportsVAE);
