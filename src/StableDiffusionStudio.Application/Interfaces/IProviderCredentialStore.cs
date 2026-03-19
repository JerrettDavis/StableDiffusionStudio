namespace StableDiffusionStudio.Application.Interfaces;

public interface IProviderCredentialStore
{
    Task<string?> GetTokenAsync(string providerId, CancellationToken ct = default);
    Task SetTokenAsync(string providerId, string token, CancellationToken ct = default);
    Task RemoveTokenAsync(string providerId, CancellationToken ct = default);
}
