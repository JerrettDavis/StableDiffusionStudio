namespace StableDiffusionStudio.Application.Interfaces;

public interface ISettingsProvider
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, CancellationToken ct = default) where T : class;
    Task<string?> GetRawAsync(string key, CancellationToken ct = default);
    Task SetRawAsync(string key, string value, CancellationToken ct = default);
}
