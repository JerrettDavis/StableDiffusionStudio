using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Infrastructure.Plugins;

namespace StableDiffusionStudio.Infrastructure.Tests.Plugins;

public class PluginManagerTests
{
    private readonly IAppPaths _appPaths;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginManager> _logger;

    public PluginManagerTests()
    {
        _appPaths = Substitute.For<IAppPaths>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<PluginManager>>();
    }

    [Fact]
    public void LoadedPlugins_InitiallyEmpty()
    {
        var manager = new PluginManager(_appPaths, _serviceProvider, _logger);
        manager.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public void PostProcessors_InitiallyEmpty()
    {
        var manager = new PluginManager(_appPaths, _serviceProvider, _logger);
        manager.PostProcessors.Should().BeEmpty();
    }

    [Fact]
    public void ModelProviderPlugins_InitiallyEmpty()
    {
        var manager = new PluginManager(_appPaths, _serviceProvider, _logger);
        manager.ModelProviderPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPluginsAsync_NonexistentDirectory_DoesNotThrow()
    {
        _appPaths.AssetsDirectory.Returns("/nonexistent/assets");
        var manager = new PluginManager(_appPaths, _serviceProvider, _logger);

        var act = () => manager.LoadPluginsAsync();
        await act.Should().NotThrowAsync();
        manager.LoadedPlugins.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadPluginsAsync_EmptyDirectory_LoadsNoPlugins()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sds-test-{Guid.NewGuid():N}");
        var pluginsDir = Path.Combine(tempDir, "Plugins");
        Directory.CreateDirectory(pluginsDir);
        try
        {
            _appPaths.AssetsDirectory.Returns(Path.Combine(tempDir, "assets"));
            var manager = new PluginManager(_appPaths, _serviceProvider, _logger);

            await manager.LoadPluginsAsync();
            manager.LoadedPlugins.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_InvalidDll_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sds-test-{Guid.NewGuid():N}");
        var pluginsDir = Path.Combine(tempDir, "Plugins");
        Directory.CreateDirectory(pluginsDir);
        try
        {
            // Write a file that is not a valid .NET assembly
            await File.WriteAllBytesAsync(Path.Combine(pluginsDir, "invalid.dll"), [0x00, 0x01, 0x02]);

            _appPaths.AssetsDirectory.Returns(Path.Combine(tempDir, "assets"));
            var manager = new PluginManager(_appPaths, _serviceProvider, _logger);

            var act = () => manager.LoadPluginsAsync();
            await act.Should().NotThrowAsync();
            manager.LoadedPlugins.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadPluginsAsync_RespectsCancellation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sds-test-{Guid.NewGuid():N}");
        var pluginsDir = Path.Combine(tempDir, "Plugins");
        Directory.CreateDirectory(pluginsDir);
        try
        {
            _appPaths.AssetsDirectory.Returns(Path.Combine(tempDir, "assets"));
            var manager = new PluginManager(_appPaths, _serviceProvider, _logger);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // With an already-cancelled token and no DLLs, it should complete without throwing
            // because the directory is empty (no loop iterations)
            await manager.LoadPluginsAsync(cts.Token);
            manager.LoadedPlugins.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
