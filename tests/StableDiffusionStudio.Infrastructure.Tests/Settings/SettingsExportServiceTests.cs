using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Settings;
using StableDiffusionStudio.Infrastructure.Tests.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Settings;

public class SettingsExportServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly SettingsExportService _service;

    public SettingsExportServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var logger = Substitute.For<ILogger<SettingsExportService>>();
        _service = new SettingsExportService(_context, logger);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ExportAllAsync_EmptyDatabase_ReturnsEmptyJsonObject()
    {
        var json = await _service.ExportAllAsync();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        dict.Should().NotBeNull();
        dict.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAllAsync_WithSettings_ReturnsAllSettings()
    {
        _context.Settings.Add(Setting.Create("key1", "value1"));
        _context.Settings.Add(Setting.Create("key2", "value2"));
        await _context.SaveChangesAsync();

        var json = await _service.ExportAllAsync();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        dict.Should().HaveCount(2);
        dict!["key1"].Should().Be("value1");
        dict["key2"].Should().Be("value2");
    }

    [Fact]
    public async Task ImportAllAsync_CreatesNewSettings()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["alpha"] = "one",
            ["beta"] = "two"
        });

        await _service.ImportAllAsync(json);

        var alpha = await _context.Settings.FindAsync("alpha");
        alpha.Should().NotBeNull();
        alpha!.Value.Should().Be("one");

        var beta = await _context.Settings.FindAsync("beta");
        beta.Should().NotBeNull();
        beta!.Value.Should().Be("two");
    }

    [Fact]
    public async Task ImportAllAsync_OverwritesExistingSettings()
    {
        _context.Settings.Add(Setting.Create("mykey", "old-value"));
        await _context.SaveChangesAsync();

        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["mykey"] = "new-value"
        });

        await _service.ImportAllAsync(json);

        var setting = await _context.Settings.FindAsync("mykey");
        setting!.Value.Should().Be("new-value");
    }

    [Fact]
    public async Task ImportAllAsync_EmptyJson_ThrowsArgumentException()
    {
        var act = () => _service.ImportAllAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ImportAllAsync_InvalidJson_ThrowsException()
    {
        var act = () => _service.ImportAllAsync("not json");
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task RoundTrip_ExportThenImport_PreservesSettings()
    {
        _context.Settings.Add(Setting.Create("setting1", "{\"nested\":true}"));
        _context.Settings.Add(Setting.Create("setting2", "simple-value"));
        await _context.SaveChangesAsync();

        var exported = await _service.ExportAllAsync();

        // Clear settings
        _context.Settings.RemoveRange(_context.Settings);
        await _context.SaveChangesAsync();

        // Import the exported data
        await _service.ImportAllAsync(exported);

        var s1 = await _context.Settings.FindAsync("setting1");
        s1!.Value.Should().Be("{\"nested\":true}");

        var s2 = await _context.Settings.FindAsync("setting2");
        s2!.Value.Should().Be("simple-value");
    }

    [Fact]
    public async Task ExportAllAsync_SettingsAreOrderedByKey()
    {
        _context.Settings.Add(Setting.Create("zebra", "z"));
        _context.Settings.Add(Setting.Create("alpha", "a"));
        _context.Settings.Add(Setting.Create("middle", "m"));
        await _context.SaveChangesAsync();

        var json = await _service.ExportAllAsync();

        // Verify the JSON keys appear in alphabetical order
        json.Should().Contain("alpha");
        var alphaIdx = json.IndexOf("alpha", StringComparison.Ordinal);
        var middleIdx = json.IndexOf("middle", StringComparison.Ordinal);
        var zebraIdx = json.IndexOf("zebra", StringComparison.Ordinal);
        alphaIdx.Should().BeLessThan(middleIdx);
        middleIdx.Should().BeLessThan(zebraIdx);
    }
}
