using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IInferenceSettingsProvider
{
    Task<InferenceSettings> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(InferenceSettings settings, CancellationToken ct = default);
}
