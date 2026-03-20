using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Settings;

public class DbInferenceSettingsProvider : IInferenceSettingsProvider
{
    private const string SettingsKey = "inference-settings";
    private readonly ISettingsProvider _settings;

    public DbInferenceSettingsProvider(ISettingsProvider settings)
    {
        _settings = settings;
    }

    public async Task<InferenceSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync<InferenceSettings>(SettingsKey, ct);
        return settings ?? InferenceSettings.Default;
    }

    public async Task SaveSettingsAsync(InferenceSettings settings, CancellationToken ct = default)
    {
        await _settings.SetAsync(SettingsKey, settings, ct);
    }
}
