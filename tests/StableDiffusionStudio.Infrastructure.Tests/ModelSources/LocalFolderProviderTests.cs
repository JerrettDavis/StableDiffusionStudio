using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.ModelSources;

namespace StableDiffusionStudio.Infrastructure.Tests.ModelSources;

public class LocalFolderProviderTests
{
    private readonly LocalFolderProvider _provider = new();
    private readonly string _fixturesPath = Path.Combine(AppContext.BaseDirectory, "ModelSources", "TestFixtures");

    [Fact]
    public async Task ScanLocalAsync_FindsModelFiles()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _provider.ScanLocalAsync(root);
        results.Should().HaveCount(2); // .safetensors and .ckpt, not .txt
    }

    [Fact]
    public async Task ScanLocalAsync_InfersFormatFromExtension()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _provider.ScanLocalAsync(root);
        results.Should().Contain(r => r.Format == ModelFormat.SafeTensors);
        results.Should().Contain(r => r.Format == ModelFormat.CKPT);
    }

    [Fact]
    public async Task ScanLocalAsync_DetectsSidecarPreview()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _provider.ScanLocalAsync(root);
        var safetensorsModel = results.First(r => r.Format == ModelFormat.SafeTensors);
        safetensorsModel.PreviewImagePath.Should().NotBeNullOrEmpty();
        safetensorsModel.PreviewImagePath.Should().EndWith("test-model.preview.png");
    }

    [Fact]
    public async Task ScanLocalAsync_ReturnsDiscoveredModels()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _provider.ScanLocalAsync(root);
        results.Should().AllBeOfType<Application.DTOs.DiscoveredModel>();
    }

    [Fact]
    public async Task ScanLocalAsync_InfersModelType()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _provider.ScanLocalAsync(root);
        // All test fixtures are small files, so they should get size-based type inference
        results.Should().OnlyContain(r => Enum.IsDefined(r.Type));
    }

    [Fact]
    public void Capabilities_ReportsLocalScanCapability()
    {
        var caps = _provider.Capabilities;
        caps.CanScanLocal.Should().BeTrue();
        caps.CanDownload.Should().BeFalse();
        caps.CanSearch.Should().BeFalse();
        caps.RequiresAuth.Should().BeFalse();
    }

    [Fact]
    public void ProviderId_ReturnsLocalFolder()
    {
        _provider.ProviderId.Should().Be("local-folder");
    }

    [Fact]
    public void DisplayName_ReturnsLocalFolder()
    {
        _provider.DisplayName.Should().Be("Local Folder");
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyResult()
    {
        var query = new Application.DTOs.ModelSearchQuery("local-folder");
        var result = await _provider.SearchAsync(query);
        result.Models.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAsync_ReturnsFailure()
    {
        var request = new Application.DTOs.DownloadRequest("local-folder", "ext-id", null,
            new StorageRoot("/tmp", "Temp"), ModelType.Checkpoint);
        var result = await _provider.DownloadAsync(request, new Progress<Application.DTOs.DownloadProgress>());
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("does not support downloads");
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ReturnsTrue()
    {
        var result = await _provider.ValidateCredentialsAsync();
        result.Should().BeTrue();
    }
}
