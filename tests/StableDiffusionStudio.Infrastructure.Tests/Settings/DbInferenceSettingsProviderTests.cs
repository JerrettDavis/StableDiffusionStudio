using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Settings;

namespace StableDiffusionStudio.Infrastructure.Tests.Settings;

public class DbInferenceSettingsProviderTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly DbInferenceSettingsProvider _provider;

    public DbInferenceSettingsProviderTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        var settingsProvider = new DbSettingsProvider(_context);
        _provider = new DbInferenceSettingsProvider(settingsProvider);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNoSettingsSaved_ReturnsDefaults()
    {
        var settings = await _provider.GetSettingsAsync();

        settings.Should().NotBeNull();
        settings.ThreadCount.Should().Be(0);
        settings.FlashAttention.Should().BeTrue();
        settings.VaeTiling.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndGet_RoundTripsSettings()
    {
        var original = new InferenceSettings
        {
            ThreadCount = 8,
            FlashAttention = false,
            DiffusionFlashAttention = false,
            VaeTiling = false,
            VaeDecodeOnly = false,
            KeepClipOnCPU = false,
            KeepVaeOnCPU = false,
            KeepControlNetOnCPU = true,
            OffloadParamsToCPU = true,
            EnableMmap = true
        };

        await _provider.SaveSettingsAsync(original);
        var loaded = await _provider.GetSettingsAsync();

        loaded.ThreadCount.Should().Be(8);
        loaded.FlashAttention.Should().BeFalse();
        loaded.DiffusionFlashAttention.Should().BeFalse();
        loaded.VaeTiling.Should().BeFalse();
        loaded.VaeDecodeOnly.Should().BeFalse();
        loaded.KeepClipOnCPU.Should().BeFalse();
        loaded.KeepVaeOnCPU.Should().BeFalse();
        loaded.KeepControlNetOnCPU.Should().BeTrue();
        loaded.OffloadParamsToCPU.Should().BeTrue();
        loaded.EnableMmap.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_OverwritesPreviousSettings()
    {
        await _provider.SaveSettingsAsync(new InferenceSettings { ThreadCount = 4 });
        await _provider.SaveSettingsAsync(new InferenceSettings { ThreadCount = 12 });

        var loaded = await _provider.GetSettingsAsync();
        loaded.ThreadCount.Should().Be(12);
    }

    [Fact]
    public async Task GetSettingsAsync_PreservesLowVRAMPreset()
    {
        await _provider.SaveSettingsAsync(InferenceSettings.LowVRAM);
        var loaded = await _provider.GetSettingsAsync();

        loaded.KeepClipOnCPU.Should().BeTrue();
        loaded.KeepVaeOnCPU.Should().BeTrue();
        loaded.KeepControlNetOnCPU.Should().BeTrue();
        loaded.OffloadParamsToCPU.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingsAsync_PreservesHighVRAMPreset()
    {
        await _provider.SaveSettingsAsync(InferenceSettings.HighVRAM);
        var loaded = await _provider.GetSettingsAsync();

        loaded.KeepClipOnCPU.Should().BeFalse();
        loaded.KeepVaeOnCPU.Should().BeFalse();
        loaded.KeepControlNetOnCPU.Should().BeFalse();
        loaded.OffloadParamsToCPU.Should().BeFalse();
        loaded.VaeTiling.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAndGet_RoundTripsAllFields()
    {
        var settings = new InferenceSettings
        {
            ThreadCount = 16,
            FlashAttention = false,
            DiffusionFlashAttention = true,
            VaeTiling = false,
            VaeDecodeOnly = true,
            KeepClipOnCPU = true,
            KeepVaeOnCPU = false,
            KeepControlNetOnCPU = true,
            OffloadParamsToCPU = false,
            EnableMmap = true
        };

        await _provider.SaveSettingsAsync(settings);
        var loaded = await _provider.GetSettingsAsync();

        loaded.ThreadCount.Should().Be(16);
        loaded.FlashAttention.Should().BeFalse();
        loaded.DiffusionFlashAttention.Should().BeTrue();
        loaded.VaeTiling.Should().BeFalse();
        loaded.VaeDecodeOnly.Should().BeTrue();
        loaded.KeepClipOnCPU.Should().BeTrue();
        loaded.KeepVaeOnCPU.Should().BeFalse();
        loaded.KeepControlNetOnCPU.Should().BeTrue();
        loaded.OffloadParamsToCPU.Should().BeFalse();
        loaded.EnableMmap.Should().BeTrue();
    }

    [Fact]
    public async Task GetSettingsAsync_DefaultSettings_HaveExpectedValues()
    {
        var settings = await _provider.GetSettingsAsync();

        settings.FlashAttention.Should().BeTrue();
        settings.DiffusionFlashAttention.Should().BeTrue();
        settings.VaeTiling.Should().BeTrue();
        settings.VaeDecodeOnly.Should().BeFalse();
        settings.KeepClipOnCPU.Should().BeTrue();
        settings.KeepVaeOnCPU.Should().BeTrue();
        settings.KeepControlNetOnCPU.Should().BeFalse();
        settings.OffloadParamsToCPU.Should().BeFalse();
        settings.EnableMmap.Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
