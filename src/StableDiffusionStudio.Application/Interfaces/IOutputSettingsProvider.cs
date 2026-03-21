using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IOutputSettingsProvider
{
    Task<OutputSettings> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(OutputSettings settings, CancellationToken ct = default);
}
