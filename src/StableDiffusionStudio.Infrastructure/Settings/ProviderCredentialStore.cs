using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Settings;

public class ProviderCredentialStore : IProviderCredentialStore
{
    private readonly ISettingsProvider _settings;
    private const string KeyPrefix = "provider-credentials:";

    public ProviderCredentialStore(ISettingsProvider settings) => _settings = settings;

    public async Task<string?> GetTokenAsync(string providerId, CancellationToken ct = default)
    {
        var raw = await _settings.GetRawAsync($"{KeyPrefix}{providerId}", ct);
        return raw;
    }

    public Task SetTokenAsync(string providerId, string token, CancellationToken ct = default)
        => _settings.SetRawAsync($"{KeyPrefix}{providerId}", token, ct);

    public async Task RemoveTokenAsync(string providerId, CancellationToken ct = default)
    {
        // Setting an empty value effectively removes the token
        await _settings.SetRawAsync($"{KeyPrefix}{providerId}", string.Empty, ct);
    }
}
