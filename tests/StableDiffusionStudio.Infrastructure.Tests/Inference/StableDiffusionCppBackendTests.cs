using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Inference;
using SdSampler = StableDiffusion.NET.Sampler;
using SdScheduler = StableDiffusion.NET.Scheduler;

namespace StableDiffusionStudio.Infrastructure.Tests.Inference;

public class StableDiffusionCppBackendTests
{
    private readonly StableDiffusionCppBackend _backend;

    public StableDiffusionCppBackendTests()
    {
        var logger = Substitute.For<ILogger<StableDiffusionCppBackend>>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        _backend = new StableDiffusionCppBackend(logger, scopeFactory);
    }

    [Fact]
    public void BackendId_IsStableDiffusionCpp()
    {
        _backend.BackendId.Should().Be("stable-diffusion-cpp");
    }

    [Fact]
    public void DisplayName_IsStableDiffusionCpp()
    {
        _backend.DisplayName.Should().Be("Stable Diffusion (C++)");
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsBool_WithoutCrashing()
    {
        // Should not throw even if native library is not present
        var result = await _backend.IsAvailableAsync();
        result.Should().Be(result); // Just assert it returns a boolean without throwing
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsSameResult_OnRepeatedCalls()
    {
        var first = await _backend.IsAvailableAsync();
        var second = await _backend.IsAvailableAsync();
        first.Should().Be(second);
    }

    [Fact]
    public void Capabilities_SupportedFamilies_ContainsExpectedValues()
    {
        _backend.Capabilities.SupportedFamilies.Should().Contain(ModelFamily.SD15);
        _backend.Capabilities.SupportedFamilies.Should().Contain(ModelFamily.SDXL);
        _backend.Capabilities.SupportedFamilies.Should().Contain(ModelFamily.Flux);
    }

    [Fact]
    public void Capabilities_SupportedSamplers_ContainsExpectedValues()
    {
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.Euler);
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.EulerA);
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.DPMPlusPlus2M);
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.DDIM);
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.Heun);
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.DPM2);
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.LMS);
        _backend.Capabilities.SupportedSamplers.Should().Contain(Sampler.UniPC);
    }

    [Fact]
    public void Capabilities_MaxDimensions_AreReasonable()
    {
        _backend.Capabilities.MaxWidth.Should().Be(2048);
        _backend.Capabilities.MaxHeight.Should().Be(2048);
    }

    [Fact]
    public void Capabilities_SupportsLoRAAndVAE()
    {
        _backend.Capabilities.SupportsLoRA.Should().BeTrue();
        _backend.Capabilities.SupportsVAE.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateAsync_WithoutModel_ReturnsFailure()
    {
        var request = new InferenceRequest("test prompt", "", Sampler.EulerA, Scheduler.Normal,
            Steps: 20, CfgScale: 7.0, Seed: 42, Width: 512, Height: 512, BatchSize: 1);
        var progress = new Progress<InferenceProgress>();

        var result = await _backend.GenerateAsync(request, progress);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("No model loaded");
    }

    [Fact]
    public async Task UnloadModelAsync_WithoutModel_DoesNotThrow()
    {
        var act = () => _backend.UnloadModelAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_WithoutModel_DoesNotThrow()
    {
        var act = () => _backend.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        _backend.Dispose();
        var act = () => _backend.Dispose();
        act.Should().NotThrow();
    }

    // Sampler mapping tests

    [Theory]
    [InlineData(Sampler.Euler, SdSampler.Euler)]
    [InlineData(Sampler.EulerA, SdSampler.Euler_A)]
    [InlineData(Sampler.DPMPlusPlus2M, SdSampler.DPMPP2M)]
    [InlineData(Sampler.DPMPlusPlus2MKarras, SdSampler.DPMPP2M)]
    [InlineData(Sampler.DPMPlusPlusSDE, SdSampler.DPMPP2SA)]
    [InlineData(Sampler.DPMPlusPlusSDEKarras, SdSampler.DPMPP2SA)]
    [InlineData(Sampler.DDIM, SdSampler.DDIM_Trailing)]
    [InlineData(Sampler.UniPC, SdSampler.IPNDM)]
    [InlineData(Sampler.LMS, SdSampler.LCM)]
    [InlineData(Sampler.Heun, SdSampler.Heun)]
    [InlineData(Sampler.DPM2, SdSampler.DPM2)]
    [InlineData(Sampler.DPM2A, SdSampler.DPM2)]
    public void MapSampler_MapsCorrectly(Sampler input, SdSampler expected)
    {
        StableDiffusionCppBackend.MapSampler(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(Scheduler.Normal, SdScheduler.Discrete)]
    [InlineData(Scheduler.Karras, SdScheduler.Karras)]
    [InlineData(Scheduler.Exponential, SdScheduler.Exponential)]
    [InlineData(Scheduler.SGMUniform, SdScheduler.SGM_Uniform)]
    public void MapScheduler_MapsCorrectly(Scheduler input, SdScheduler expected)
    {
        StableDiffusionCppBackend.MapScheduler(input).Should().Be(expected);
    }
}
