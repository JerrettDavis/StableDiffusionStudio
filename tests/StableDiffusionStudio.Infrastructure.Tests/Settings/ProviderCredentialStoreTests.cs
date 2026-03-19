using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Settings;

namespace StableDiffusionStudio.Infrastructure.Tests.Settings;

public class ProviderCredentialStoreTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ProviderCredentialStore _store;

    public ProviderCredentialStoreTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        var settingsProvider = new DbSettingsProvider(_context);
        _store = new ProviderCredentialStore(settingsProvider);
    }

    [Fact]
    public async Task SetAndGetToken_RoundTrips()
    {
        await _store.SetTokenAsync("huggingface", "hf_test_token_123");

        var result = await _store.GetTokenAsync("huggingface");
        result.Should().Be("hf_test_token_123");
    }

    [Fact]
    public async Task GetToken_WhenMissing_ReturnsNull()
    {
        var result = await _store.GetTokenAsync("nonexistent-provider");
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveToken_RemovesStoredToken()
    {
        await _store.SetTokenAsync("civitai", "civit_key_456");
        var before = await _store.GetTokenAsync("civitai");
        before.Should().Be("civit_key_456");

        await _store.RemoveTokenAsync("civitai");

        var after = await _store.GetTokenAsync("civitai");
        after.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SetToken_OverwritesExistingToken()
    {
        await _store.SetTokenAsync("huggingface", "old_token");
        await _store.SetTokenAsync("huggingface", "new_token");

        var result = await _store.GetTokenAsync("huggingface");
        result.Should().Be("new_token");
    }

    [Fact]
    public async Task MultipleProviders_AreIndependent()
    {
        await _store.SetTokenAsync("huggingface", "hf_token");
        await _store.SetTokenAsync("civitai", "civit_token");

        var hf = await _store.GetTokenAsync("huggingface");
        var civit = await _store.GetTokenAsync("civitai");

        hf.Should().Be("hf_token");
        civit.Should().Be("civit_token");
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
