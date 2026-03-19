using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Infrastructure.Inference;

public class StableDiffusionCppBackend : IInferenceBackend
{
    public string BackendId => "stable-diffusion-cpp";
    public string DisplayName => "Stable Diffusion (C++)";
    public InferenceCapabilities Capabilities => new(
        SupportedFamilies: [ModelFamily.SD15, ModelFamily.SDXL],
        SupportedSamplers: [Sampler.Euler, Sampler.EulerA, Sampler.DPMPlusPlus2M, Sampler.DDIM],
        MaxWidth: 2048, MaxHeight: 2048,
        SupportsLoRA: true, SupportsVAE: true);

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("StableDiffusion.NET is not available. Use Mock backend.");
    public Task<InferenceResult> GenerateAsync(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct = default)
        => throw new NotSupportedException("StableDiffusion.NET is not available.");
    public Task UnloadModelAsync(CancellationToken ct = default) => Task.CompletedTask;
}
