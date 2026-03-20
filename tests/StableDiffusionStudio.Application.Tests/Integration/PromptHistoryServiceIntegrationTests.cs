using FluentAssertions;
using Microsoft.Data.Sqlite;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Application.Tests.Integration;

public class PromptHistoryServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly PromptHistoryService _service;

    public PromptHistoryServiceIntegrationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var repo = new PromptHistoryRepository(_context);
        _service = new PromptHistoryService(repo);
    }

    [Fact]
    public async Task RecordUsage_CreatesNewEntry()
    {
        await _service.RecordUsageAsync("a beautiful sunset", "ugly");

        var recent = await _service.ListRecentAsync();
        recent.Should().HaveCount(1);
        recent[0].PositivePrompt.Should().Be("a beautiful sunset");
        recent[0].NegativePrompt.Should().Be("ugly");
        recent[0].UseCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordUsage_SamePrompts_IncrementsCount()
    {
        await _service.RecordUsageAsync("sunset", "ugly");
        await _service.RecordUsageAsync("sunset", "ugly");
        await _service.RecordUsageAsync("sunset", "ugly");

        var recent = await _service.ListRecentAsync();
        recent.Should().HaveCount(1);
        recent[0].UseCount.Should().Be(3);
    }

    [Fact]
    public async Task RecordUsage_EmptyPrompt_DoesNothing()
    {
        await _service.RecordUsageAsync("", "negative");
        await _service.RecordUsageAsync("  ", "negative");

        var recent = await _service.ListRecentAsync();
        recent.Should().BeEmpty();
    }

    [Fact]
    public async Task ListRecentAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _service.RecordUsageAsync($"prompt {i}", "");
            await Task.Delay(5); // ensure ordering
        }

        var recent = await _service.ListRecentAsync(take: 3);
        recent.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_FindsMatchingPrompts()
    {
        await _service.RecordUsageAsync("a beautiful sunset over the ocean", "");
        await _service.RecordUsageAsync("a cat sitting on a mat", "");
        await _service.RecordUsageAsync("sunset in the mountains", "");

        var results = await _service.SearchAsync("sunset");
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_FindsMatchInNegativePrompt()
    {
        await _service.RecordUsageAsync("a cat", "ugly deformed");
        await _service.RecordUsageAsync("a dog", "blurry");

        var results = await _service.SearchAsync("ugly");
        results.Should().HaveCount(1);
        results[0].PositivePrompt.Should().Be("a cat");
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        await _service.RecordUsageAsync("deletable prompt", "");

        var recent = await _service.ListRecentAsync();
        recent.Should().HaveCount(1);

        await _service.DeleteAsync(recent[0].Id);

        var afterDelete = await _service.ListRecentAsync();
        afterDelete.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordUsage_DifferentNegative_CreatesSeparateEntry()
    {
        await _service.RecordUsageAsync("same positive", "negative A");
        await _service.RecordUsageAsync("same positive", "negative B");

        var recent = await _service.ListRecentAsync();
        recent.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
