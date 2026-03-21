using FluentAssertions;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Application.Tests.Interfaces;

public class PluginInterfaceTests
{
    [Fact]
    public void IPlugin_HasRequiredMembers()
    {
        var type = typeof(IPlugin);
        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("Name").Should().NotBeNull();
        type.GetProperty("Version").Should().NotBeNull();
        type.GetProperty("Description").Should().NotBeNull();
        type.GetMethod("InitializeAsync").Should().NotBeNull();
        type.GetMethod("ShutdownAsync").Should().NotBeNull();
    }

    [Fact]
    public void IPostProcessor_ExtendsIPlugin()
    {
        typeof(IPostProcessor).Should().Implement<IPlugin>();
    }

    [Fact]
    public void IPostProcessor_HasProcessAsyncMethod()
    {
        typeof(IPostProcessor).GetMethod("ProcessAsync").Should().NotBeNull();
    }

    [Fact]
    public void IModelProviderPlugin_ExtendsIPlugin()
    {
        typeof(IModelProviderPlugin).Should().Implement<IPlugin>();
    }

    [Fact]
    public void IModelProviderPlugin_ExtendsIModelProvider()
    {
        typeof(IModelProviderPlugin).Should().Implement<IModelProvider>();
    }

    [Fact]
    public void IPluginManager_HasRequiredMembers()
    {
        var type = typeof(IPluginManager);
        type.GetProperty("LoadedPlugins").Should().NotBeNull();
        type.GetProperty("PostProcessors").Should().NotBeNull();
        type.GetProperty("ModelProviderPlugins").Should().NotBeNull();
        type.GetMethod("LoadPluginsAsync").Should().NotBeNull();
    }
}
