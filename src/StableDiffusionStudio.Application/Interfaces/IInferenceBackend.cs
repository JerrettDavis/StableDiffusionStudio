using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IInferenceBackend
{
    string BackendId { get; }
    string DisplayName { get; }
    InferenceCapabilities Capabilities { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default);
    Task<InferenceResult> GenerateAsync(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct = default);
    Task UnloadModelAsync(CancellationToken ct = default);
}
