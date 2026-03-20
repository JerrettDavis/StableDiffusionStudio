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

    [Fact]
    public async Task GetAsync_NonExistent_ReturnsNull()
    {
        var result = await _provider.GetAsync<TestData>("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_OverwritesExisting()
    {
        await _provider.SetAsync("key", new TestData("first", 1));
        await _provider.SetAsync("key", new TestData("second", 2));

        var result = await _provider.GetAsync<TestData>("key");
        result.Should().NotBeNull();
        result!.Name.Should().Be("second");
        result.Value.Should().Be(2);
    }

    [Fact]
    public async Task MultipleKeys_DoNotInterfere()
    {
        await _provider.SetAsync("key-a", new TestData("A", 1));
        await _provider.SetAsync("key-b", new TestData("B", 2));
        await _provider.SetAsync("key-c", new TestData("C", 3));

        var a = await _provider.GetAsync<TestData>("key-a");
        var b = await _provider.GetAsync<TestData>("key-b");
        var c = await _provider.GetAsync<TestData>("key-c");

        a!.Name.Should().Be("A");
        b!.Name.Should().Be("B");
        c!.Name.Should().Be("C");
    }

    [Fact]
    public async Task GetAsync_WithNestedObject_RoundTrips()
    {
        var data = new NestedTestData("outer", new TestData("inner", 42), new[] { "tag1", "tag2" });
        await _provider.SetAsync("nested", data);

        var result = await _provider.GetAsync<NestedTestData>("nested");
        result.Should().NotBeNull();
        result!.Label.Should().Be("outer");
        result.Inner.Name.Should().Be("inner");
        result.Inner.Value.Should().Be(42);
        result.Tags.Should().Contain("tag1");
        result.Tags.Should().Contain("tag2");
    }

    [Fact]
    public async Task SetRawAsync_ThenGetAsync_Deserializes()
    {
        await _provider.SetRawAsync("manual", """{"Name":"manual","Value":99}""");

        var result = await _provider.GetAsync<TestData>("manual");
        result.Should().NotBeNull();
        result!.Name.Should().Be("manual");
        result.Value.Should().Be(99);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private record TestData(string Name, int Value);
    private record NestedTestData(string Label, TestData Inner, string[] Tags);
}
