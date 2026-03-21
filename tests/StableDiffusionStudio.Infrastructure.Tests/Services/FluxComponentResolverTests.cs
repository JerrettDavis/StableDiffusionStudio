using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Services;

namespace StableDiffusionStudio.Infrastructure.Tests.Services;

public class FluxComponentResolverTests : IDisposable
{
    private readonly IStorageRootProvider _rootProvider = Substitute.For<IStorageRootProvider>();
    private readonly ILogger<FluxComponentResolver> _logger = Substitute.For<ILogger<FluxComponentResolver>>();
    private readonly FluxComponentResolver _resolver;
    private readonly string _tempDir;

    public FluxComponentResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SDS_FluxTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _resolver = new FluxComponentResolver(_rootProvider, _logger);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { /* best effort cleanup */ }
    }

    private void SetupStorageRoots(params StorageRoot[] roots)
    {
        _rootProvider.GetRootsAsync(Arg.Any<CancellationToken>())
            .Returns(roots.ToList().AsReadOnly());
    }

    [Fact]
    public async Task ResolveAsync_FindsVaeInSiblingVaeDirectory()
    {
        // Arrange: models/Stable-diffusion/flux.gguf + models/VAE/ae.safetensors
        var sdDir = Path.Combine(_tempDir, "models", "Stable-diffusion");
        var vaeDir = Path.Combine(_tempDir, "models", "VAE");
        Directory.CreateDirectory(sdDir);
        Directory.CreateDirectory(vaeDir);
        var modelPath = Path.Combine(sdDir, "flux1-dev-Q8_0.gguf");
        File.WriteAllText(modelPath, "fake");
        File.WriteAllText(Path.Combine(vaeDir, "ae.safetensors"), "fake");

        SetupStorageRoots();

        // Act
        var result = await _resolver.ResolveAsync(modelPath);

        // Assert
        result.Should().NotBeNull();
        result!.VaePath.Should().EndWith("ae.safetensors");
    }

    [Fact]
    public async Task ResolveAsync_FindsClipLInSiblingTextEncoderDirectory()
    {
        var sdDir = Path.Combine(_tempDir, "models", "Stable-diffusion");
        var teDir = Path.Combine(_tempDir, "models", "text_encoder");
        Directory.CreateDirectory(sdDir);
        Directory.CreateDirectory(teDir);
        var modelPath = Path.Combine(sdDir, "flux1-dev-Q8_0.gguf");
        File.WriteAllText(modelPath, "fake");
        File.WriteAllText(Path.Combine(teDir, "clip_l.safetensors"), "fake");

        SetupStorageRoots();

        var result = await _resolver.ResolveAsync(modelPath);

        result.Should().NotBeNull();
        result!.ClipLPath.Should().EndWith("clip_l.safetensors");
    }

    [Fact]
    public async Task ResolveAsync_FindsT5xxlGguf_PreferredOverSafetensors()
    {
        var sdDir = Path.Combine(_tempDir, "models", "Stable-diffusion");
        var teDir = Path.Combine(_tempDir, "models", "text_encoder");
        Directory.CreateDirectory(sdDir);
        Directory.CreateDirectory(teDir);
        var modelPath = Path.Combine(sdDir, "flux1-dev-Q8_0.gguf");
        File.WriteAllText(modelPath, "fake");
        // Create both GGUF Q8 and safetensors; Q8 GGUF should be preferred
        File.WriteAllText(Path.Combine(teDir, "t5-v1_1-xxl-encoder-Q8_0.gguf"), "fake");
        File.WriteAllText(Path.Combine(teDir, "t5xxl_fp16.safetensors"), "fake");

        SetupStorageRoots();

        var result = await _resolver.ResolveAsync(modelPath);

        result.Should().NotBeNull();
        result!.T5xxlPath.Should().Contain("Q8_0.gguf");
    }

    [Fact]
    public async Task ResolveAsync_FindsAllComponents()
    {
        var sdDir = Path.Combine(_tempDir, "models", "Stable-diffusion");
        var vaeDir = Path.Combine(_tempDir, "models", "VAE");
        var teDir = Path.Combine(_tempDir, "models", "text_encoder");
        Directory.CreateDirectory(sdDir);
        Directory.CreateDirectory(vaeDir);
        Directory.CreateDirectory(teDir);
        var modelPath = Path.Combine(sdDir, "flux1-dev-Q8_0.gguf");
        File.WriteAllText(modelPath, "fake");
        File.WriteAllText(Path.Combine(vaeDir, "ae.safetensors"), "fake");
        File.WriteAllText(Path.Combine(teDir, "clip_l.safetensors"), "fake");
        File.WriteAllText(Path.Combine(teDir, "t5-v1_1-xxl-encoder-Q8_0.gguf"), "fake");

        SetupStorageRoots();

        var result = await _resolver.ResolveAsync(modelPath);

        result.Should().NotBeNull();
        result!.VaePath.Should().NotBeNull();
        result.ClipLPath.Should().NotBeNull();
        result.T5xxlPath.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_NoComponentsFound_ReturnsNull()
    {
        var sdDir = Path.Combine(_tempDir, "models", "Stable-diffusion");
        Directory.CreateDirectory(sdDir);
        var modelPath = Path.Combine(sdDir, "flux1-dev-Q8_0.gguf");
        File.WriteAllText(modelPath, "fake");

        SetupStorageRoots();

        var result = await _resolver.ResolveAsync(modelPath);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_FindsComponentsViaStorageRoot()
    {
        var rootDir = Path.Combine(_tempDir, "myroot");
        var vaeDir = Path.Combine(_tempDir, "VAE"); // sibling of myroot parent = _tempDir/VAE
        // Actually: parent of rootDir is _tempDir, so parentDir/VAE = _tempDir/VAE
        Directory.CreateDirectory(rootDir);
        Directory.CreateDirectory(vaeDir);
        File.WriteAllText(Path.Combine(vaeDir, "ae.safetensors"), "fake");

        var modelPath = Path.Combine(rootDir, "flux1-dev-Q8_0.gguf");
        File.WriteAllText(modelPath, "fake");

        SetupStorageRoots(new StorageRoot(rootDir, "Models"));

        var result = await _resolver.ResolveAsync(modelPath);

        result.Should().NotBeNull();
        result!.VaePath.Should().EndWith("ae.safetensors");
    }

    [Fact]
    public async Task ResolveAsync_HandlesNonExistentStorageRootGracefully()
    {
        var nonExistent = Path.Combine(_tempDir, "does_not_exist");
        SetupStorageRoots(new StorageRoot(nonExistent, "Ghost"));

        var modelPath = Path.Combine(_tempDir, "flux.gguf");

        // Should not throw
        var result = await _resolver.ResolveAsync(modelPath);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_FindsT5xxlSafetensorsWhenNoGguf()
    {
        var sdDir = Path.Combine(_tempDir, "models", "Stable-diffusion");
        var teDir = Path.Combine(_tempDir, "models", "text_encoder");
        Directory.CreateDirectory(sdDir);
        Directory.CreateDirectory(teDir);
        var modelPath = Path.Combine(sdDir, "flux1-dev-Q8_0.gguf");
        File.WriteAllText(modelPath, "fake");
        File.WriteAllText(Path.Combine(teDir, "t5xxl_fp16.safetensors"), "fake");

        SetupStorageRoots();

        var result = await _resolver.ResolveAsync(modelPath);

        result.Should().NotBeNull();
        result!.T5xxlPath.Should().Contain("t5xxl_fp16.safetensors");
    }
}
