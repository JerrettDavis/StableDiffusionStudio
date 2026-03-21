namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Resolves Flux model component paths (VAE, CLIP-L, T5-XXL) by scanning
/// configured storage roots and common directory structures.
/// </summary>
public interface IFluxComponentResolver
{
    Task<FluxComponents?> ResolveAsync(string modelPath, CancellationToken ct = default);
}

public sealed record FluxComponents(
    string? VaePath,
    string? ClipLPath,
    string? T5xxlPath);
