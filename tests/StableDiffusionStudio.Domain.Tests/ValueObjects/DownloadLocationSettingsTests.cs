using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class DownloadLocationSettingsTests
{
    [Fact]
    public void Default_HasEmptyProviderRoots()
    {
        var settings = DownloadLocationSettings.Default;
        settings.ProviderRoots.Should().BeEmpty();
    }

    [Theory]
    [InlineData(ModelType.Checkpoint, "Checkpoints")]
    [InlineData(ModelType.LoRA, "LoRA")]
    [InlineData(ModelType.VAE, "VAE")]
    [InlineData(ModelType.Embedding, "Embeddings")]
    [InlineData(ModelType.ControlNet, "ControlNet")]
    public void GetDownloadPath_ReturnsCorrectSubfolder_PerModelType(ModelType type, string expectedSubfolder)
    {
        var settings = DownloadLocationSettings.Default;
        var path = settings.GetDownloadPath("civitai", type, "/default/root");
        path.Should().EndWith(expectedSubfolder);
    }

    [Fact]
    public void GetDownloadPath_UnknownType_ReturnsOtherSubfolder()
    {
        var settings = DownloadLocationSettings.Default;
        var path = settings.GetDownloadPath("civitai", ModelType.Unknown, "/default/root");
        path.Should().EndWith("Other");
    }

    [Fact]
    public void GetDownloadPath_UsesDefaultRoot_WhenProviderNotConfigured()
    {
        var settings = DownloadLocationSettings.Default;
        var path = settings.GetDownloadPath("civitai", ModelType.Checkpoint, "/default/root");
        path.Should().StartWith("/default/root");
    }

    [Fact]
    public void GetDownloadPath_UsesCustomRoot_WhenProviderConfigured()
    {
        var settings = new DownloadLocationSettings
        {
            ProviderRoots = new Dictionary<string, string>
            {
                ["civitai"] = "/custom/civitai"
            }
        };

        var path = settings.GetDownloadPath("civitai", ModelType.LoRA, "/default/root");
        path.Should().StartWith("/custom/civitai");
        path.Should().EndWith("LoRA");
    }

    [Fact]
    public void GetDownloadPath_FallsBackToDefault_ForUnconfiguredProvider()
    {
        var settings = new DownloadLocationSettings
        {
            ProviderRoots = new Dictionary<string, string>
            {
                ["civitai"] = "/custom/civitai"
            }
        };

        var path = settings.GetDownloadPath("huggingface", ModelType.Checkpoint, "/default/root");
        path.Should().StartWith("/default/root");
    }

    [Fact]
    public void GetDownloadPath_Upscaler_ReturnsOtherSubfolder()
    {
        var settings = DownloadLocationSettings.Default;
        var path = settings.GetDownloadPath("civitai", ModelType.Upscaler, "/root");
        path.Should().EndWith("Other");
    }
}
