namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Base interface for all plugins in Stable Diffusion Studio.
/// Plugins are loaded from assemblies in the Plugins directory.
/// </summary>
public interface IPlugin
{
    /// <summary>Unique identifier for the plugin.</summary>
    string Id { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>Semantic version string (e.g., "1.0.0").</summary>
    string Version { get; }

    /// <summary>Brief description of what the plugin does.</summary>
    string Description { get; }

    /// <summary>
    /// Called when the plugin is loaded. Use to register services or initialize state.
    /// </summary>
    Task InitializeAsync(IServiceProvider services, CancellationToken ct = default);

    /// <summary>
    /// Called when the plugin is being unloaded or the application is shutting down.
    /// </summary>
    Task ShutdownAsync(CancellationToken ct = default);
}

/// <summary>
/// A plugin that can post-process generated images (e.g., upscaling, style transfer, watermarking).
/// </summary>
public interface IPostProcessor : IPlugin
{
    /// <summary>
    /// Processes an image and returns the modified image bytes.
    /// </summary>
    /// <param name="imageBytes">The input image as PNG bytes.</param>
    /// <param name="parameters">Plugin-specific configuration parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The processed image as PNG bytes.</returns>
    Task<byte[]> ProcessAsync(byte[] imageBytes, Dictionary<string, object> parameters, CancellationToken ct = default);
}

/// <summary>
/// A plugin that provides an additional model source (e.g., a custom model repository).
/// </summary>
public interface IModelProviderPlugin : IPlugin, IModelProvider { }
