using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Plugins;

public class PluginManager : IPluginManager
{
    private readonly List<IPlugin> _plugins = [];
    private readonly IAppPaths _appPaths;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;

    public IReadOnlyList<IPlugin> LoadedPlugins => _plugins;

    public IReadOnlyList<IPostProcessor> PostProcessors =>
        _plugins.OfType<IPostProcessor>().ToList();

    public IReadOnlyList<IModelProviderPlugin> ModelProviderPlugins =>
        _plugins.OfType<IModelProviderPlugin>().ToList();

    public PluginManager(IAppPaths appPaths, IServiceProvider serviceProvider, ILogger<PluginManager> logger)
    {
        _appPaths = appPaths;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task LoadPluginsAsync(CancellationToken ct = default)
    {
        var pluginsDir = GetPluginsDirectory();
        if (!Directory.Exists(pluginsDir))
        {
            _logger.LogInformation("Plugins directory does not exist: {Dir} — no plugins loaded", pluginsDir);
            return;
        }

        var dlls = Directory.GetFiles(pluginsDir, "*.dll");
        _logger.LogInformation("Scanning {Count} DLL(s) in plugins directory: {Dir}", dlls.Length, pluginsDir);

        foreach (var dll in dlls)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

                foreach (var type in pluginTypes)
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IPlugin plugin)
                        {
                            await plugin.InitializeAsync(_serviceProvider, ct);
                            _plugins.Add(plugin);
                            _logger.LogInformation("Loaded plugin: {Id} ({Name} v{Version})",
                                plugin.Id, plugin.Name, plugin.Version);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to instantiate plugin type {Type} from {Dll}", type.FullName, dll);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load assembly: {Dll}", dll);
            }
        }

        _logger.LogInformation("Plugin loading complete: {Count} plugin(s) loaded", _plugins.Count);
    }

    private string GetPluginsDirectory()
    {
        // Plugins directory is a sibling of the Assets directory
        var assetsParent = Path.GetDirectoryName(_appPaths.AssetsDirectory) ?? _appPaths.AssetsDirectory;
        return Path.Combine(assetsParent, "Plugins");
    }
}
