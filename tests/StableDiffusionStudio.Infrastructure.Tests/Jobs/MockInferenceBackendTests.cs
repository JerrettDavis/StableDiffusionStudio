using FluentAssertions;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Jobs;

namespace StableDiffusionStudio.Infrastructure.Tests.Jobs;

public class MockInferenceBackendTests
{
    private readonly MockInferenceBackend _backend = new();

    [Fact]
    public void BackendId_IsMock()
    {
        _backend.BackendId.Should().Be("mock");
    }

    [Fact]
    public void DisplayName_IsMockTesting()
    {
        _backend.DisplayName.Should().Be("Mock (Testing)");
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue()
    {
        var result = await _backend.IsAvailableAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LoadModelAsync_DoesNotThrow()
    {
        var request = new ModelLoadRequest("/models/test.safetensors", null, []);
        var act = () => _backend.LoadModelAsync(request);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GenerateAsync_ReturnsSingleImage_WithDefaultBatchSize()
    {
        var request = new InferenceRequest("test prompt", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 2, CfgScale: 7.0, Seed: 42, Width: 512, Height: 512, BatchSize: 1);
        var progress = new Progress<InferenceProgress>();

        var result = await _backend.GenerateAsync(request, progress);

        result.Success.Should().BeTrue();
        result.Images.Should().HaveCount(1);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task GenerateAsync_ReturnsMultipleImages_ForBatchSize()
    {
        var request = new InferenceRequest("test prompt", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 2, CfgScale: 7.0, Seed: 42, Width: 512, Height: 512, BatchSize: 3);
        var progress = new Progress<InferenceProgress>();

        var result = await _backend.GenerateAsync(request, progress);

        result.Success.Should().BeTrue();
        result.Images.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateAsync_SeedIsPreserved()
    {
        var request = new InferenceRequest("test", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 2, CfgScale: 7.0, Seed: 12345, Width: 512, Height: 512, BatchSize: 1);
        var progress = new Progress<InferenceProgress>();

        var result = await _backend.GenerateAsync(request, progress);

        result.Images[0].Seed.Should().Be(12345);
    }

    [Fact]
    public async Task GenerateAsync_BatchSeedIncrementsPerImage()
    {
        var request = new InferenceRequest("test", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 2, CfgScale: 7.0, Seed: 100, Width: 512, Height: 512, BatchSize: 3);
        var progress = new Progress<InferenceProgress>();

        var result = await _backend.GenerateAsync(request, progress);

        result.Images[0].Seed.Should().Be(100);
        result.Images[1].Seed.Should().Be(101);
        result.Images[2].Seed.Should().Be(102);
    }

    [Fact]
    public async Task GenerateAsync_ReportsProgress()
    {
        var progressReports = new List<InferenceProgress>();
        var request = new InferenceRequest("test", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 3, CfgScale: 7.0, Seed: 42, Width: 512, Height: 512, BatchSize: 1);
        var progress = new Progress<InferenceProgress>(p => progressReports.Add(p));

        await _backend.GenerateAsync(request, progress);

        // Allow time for progress callbacks
        await Task.Delay(100);
        progressReports.Should().HaveCount(3);
        progressReports[0].Step.Should().Be(1);
        progressReports[2].Step.Should().Be(3);
        progressReports[2].TotalSteps.Should().Be(3);
    }

    [Fact]
    public async Task GenerateAsync_ImageBytesAreValidPng()
    {
        var request = new InferenceRequest("test", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 2, CfgScale: 7.0, Seed: 42, Width: 512, Height: 512, BatchSize: 1);
        var progress = new Progress<InferenceProgress>();

        var result = await _backend.GenerateAsync(request, progress);

        var bytes = result.Images[0].ImageBytes;
        bytes.Should().NotBeNullOrEmpty();
        // Verify PNG signature
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50); // P
        bytes[2].Should().Be(0x4E); // N
        bytes[3].Should().Be(0x47); // G
    }

    [Fact]
    public async Task GenerateAsync_RecordsGenerationTime()
    {
        var request = new InferenceRequest("test", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 2, CfgScale: 7.0, Seed: 42, Width: 512, Height: 512, BatchSize: 1);
        var progress = new Progress<InferenceProgress>();

        var result = await _backend.GenerateAsync(request, progress);

        result.Images[0].GenerationTimeSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_CanBeCancelled()
    {
        var cts = new CancellationTokenSource();
        var request = new InferenceRequest("test", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 100, CfgScale: 7.0, Seed: 42, Width: 512, Height: 512, BatchSize: 1);
        var progress = new Progress<InferenceProgress>();

        cts.CancelAfter(100);
        var act = () => _backend.GenerateAsync(request, progress, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void CreateMinimalPng_ProducesValidPng()
    {
        var bytes = MockInferenceBackend.CreateMinimalPng(1, 1, 42);
        bytes.Should().NotBeNullOrEmpty();
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        bytes[..8].Should().BeEquivalentTo(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    }

    [Fact]
    public void CreateMinimalPng_DifferentSeeds_ProduceDifferentImages()
    {
        var png1 = MockInferenceBackend.CreateMinimalPng(1, 1, 1);
        var png2 = MockInferenceBackend.CreateMinimalPng(1, 1, 256);
        png1.Should().NotBeEquivalentTo(png2);
    }

    [Fact]
    public void Capabilities_SupportedFamilies_ContainsExpectedValues()
    {
        _backend.Capabilities.SupportedFamilies.Should().Contain(ModelFamily.SD15);
        _backend.Capabilities.SupportedFamilies.Should().Contain(ModelFamily.SDXL);
        _backend.Capabilities.SupportsLoRA.Should().BeTrue();
        _backend.Capabilities.SupportsVAE.Should().BeTrue();
    }

    [Fact]
    public async Task UnloadModelAsync_DoesNotThrow()
    {
        var act = () => _backend.UnloadModelAsync();
        await act.Should().NotThrowAsync();
    }
}
