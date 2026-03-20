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

    [Fact]
    public async Task ScanLocalAsync_WithModelTypeTag_OverridesInferredType()
    {
        var root = new StorageRoot(_fixturesPath, "LoRA Models", ModelType.LoRA);
        var results = await _provider.ScanLocalAsync(root);

        // All models should have the overridden type
        results.Should().OnlyContain(r => r.Type == ModelType.LoRA);
    }

    [Fact]
    public async Task ScanLocalAsync_NonExistentDirectory_ReturnsEmpty()
    {
        var root = new StorageRoot("/nonexistent/path/that/does/not/exist", "Nonexistent");
        var results = await _provider.ScanLocalAsync(root);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanLocalAsync_DiscoveredModel_HasCorrectFields()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _provider.ScanLocalAsync(root);

        var model = results.First();
        model.FilePath.Should().NotBeNullOrEmpty();
        model.FileSize.Should().BeGreaterThanOrEqualTo(0);
        model.Tags.Should().NotBeNull();
        Enum.IsDefined(model.Format).Should().BeTrue();
        Enum.IsDefined(model.Family).Should().BeTrue();
        Enum.IsDefined(model.Type).Should().BeTrue();
    }

    [Fact]
    public async Task ScanLocalAsync_IgnoresNonModelFiles()
    {
        var root = new StorageRoot(_fixturesPath, "Test Models");
        var results = await _provider.ScanLocalAsync(root);

        // Should not include .txt or .png files
        results.Should().NotContain(r => r.FilePath.EndsWith(".txt"));
        results.Should().NotContain(r => r.FilePath.EndsWith(".png"));
    }

    [Fact]
    public void Capabilities_ReportsCorrectModelTypes()
    {
        var caps = _provider.Capabilities;
        caps.SupportedModelTypes.Should().Contain(ModelType.Checkpoint);
        caps.SupportedModelTypes.Should().Contain(ModelType.LoRA);
        caps.SupportedModelTypes.Should().Contain(ModelType.VAE);
    }
}
