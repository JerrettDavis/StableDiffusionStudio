using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Settings;

namespace StableDiffusionStudio.Infrastructure.Tests.Settings;

public class DbSettingsProviderTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly DbSettingsProvider _provider;

    public DbSettingsProviderTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _context = new AppDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        _provider = new DbSettingsProvider(_context);
    }

    [Fact]
    public async Task GetRawAsync_WhenKeyDoesNotExist_ReturnsNull()
    {
        var result = await _provider.GetRawAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetRawAsync_ThenGetRawAsync_ReturnsValue()
    {
        await _provider.SetRawAsync("test-key", "test-value");

        var result = await _provider.GetRawAsync("test-key");
        result.Should().Be("test-value");
    }

    [Fact]
    public async Task SetRawAsync_UpdatesExistingValue()
    {
        await _provider.SetRawAsync("test-key", "original");
        await _provider.SetRawAsync("test-key", "updated");

        var result = await _provider.GetRawAsync("test-key");
        result.Should().Be("updated");
    }

    [Fact]
    public async Task GetAsync_SetAsync_RoundTripsComplexObject()
    {
        var data = new TestData("hello", 42);
        await _provider.SetAsync("complex-key", data);

        var result = await _provider.GetAsync<TestData>("complex-key");
        result.Should().NotBeNull();
        result!.Name.Should().Be("hello");
        result.Value.Should().Be(42);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private record TestData(string Name, int Value);
}
