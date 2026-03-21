using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Settings;

public class DbOutputSettingsProvider : IOutputSettingsProvider
{
    private const string SettingsKey = "output-settings";
    private readonly ISettingsProvider _settings;

    public DbOutputSettingsProvider(ISettingsProvider settings)
    {
        _settings = settings;
    }

    public async Task<OutputSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync<OutputSettings>(SettingsKey, ct);
        return settings ?? OutputSettings.Default;
    }

    public async Task SaveSettingsAsync(OutputSettings settings, CancellationToken ct = default)
    {
        await _settings.SetAsync(SettingsKey, settings, ct);
    }
}
