namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Manages the lifecycle of plugins: discovery, loading, and access.
/// </summary>
public interface IPluginManager
{
    /// <summary>All currently loaded plugins.</summary>
    IReadOnlyList<IPlugin> LoadedPlugins { get; }

    /// <summary>All loaded post-processor plugins.</summary>
    IReadOnlyList<IPostProcessor> PostProcessors { get; }

    /// <summary>All loaded model provider plugins.</summary>
    IReadOnlyList<IModelProviderPlugin> ModelProviderPlugins { get; }

    /// <summary>
    /// Scans the plugins directory and loads all valid plugin assemblies.
    /// </summary>
    Task LoadPluginsAsync(CancellationToken ct = default);
}
