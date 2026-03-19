using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.ModelSources;

namespace StableDiffusionStudio.Infrastructure.Tests.ModelSources;

public class LocalFolderAdapterTests
{
    private readonly LocalFolderAdapter _adapter = new();
    private readonly string _fixturesPath = Path.Combine(AppContext.BaseDirectory, "ModelSources", "TestFixtures");

    [Fact]
    public async Task ScanAsync_FindsModelFiles()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _adapter.ScanAsync(root);
        results.Should().HaveCount(2); // .safetensors and .ckpt, not .txt
    }

    [Fact]
    public async Task ScanAsync_InfersFormatFromExtension()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _adapter.ScanAsync(root);
        results.Should().Contain(r => r.Format == ModelFormat.SafeTensors);
        results.Should().Contain(r => r.Format == ModelFormat.CKPT);
    }

    [Fact]
    public async Task ScanAsync_DetectsSidecarPreview()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _adapter.ScanAsync(root);
        var safetensorsModel = results.First(r => r.Format == ModelFormat.SafeTensors);
        safetensorsModel.PreviewImagePath.Should().NotBeNullOrEmpty();
        safetensorsModel.PreviewImagePath.Should().EndWith("test-model.preview.png");
    }

    [Fact]
    public async Task ScanAsync_SetsSourceName()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _adapter.ScanAsync(root);
        results.Should().OnlyContain(r => r.Source == "local-folder");
    }

    [Fact]
    public void GetCapabilities_ReturnsLocalScanCapability()
    {
        var caps = _adapter.GetCapabilities();
        caps.CanScanLocal.Should().BeTrue();
        caps.CanDownload.Should().BeFalse();
    }

    [Fact]
    public void SourceName_ReturnsLocalFolder()
    {
        _adapter.SourceName.Should().Be("local-folder");
    }
}
