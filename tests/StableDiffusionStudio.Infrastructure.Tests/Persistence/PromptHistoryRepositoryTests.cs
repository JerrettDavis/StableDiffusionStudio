using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public class PromptHistoryRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly PromptHistoryRepository _repo;

    public PromptHistoryRepositoryTests()
    {
        var (context, connection) = TestDbContextFactory.Create();
        _context = context;
        _connection = connection;
        _repo = new PromptHistoryRepository(context);
    }

    [Fact]
    public async Task UpsertAsync_AddsNewEntry()
    {
        var entry = PromptHistory.Create("sunset over mountains", "blurry");

        await _repo.UpsertAsync(entry);

        var all = await _repo.ListRecentAsync();
        all.Should().ContainSingle();
        all[0].PositivePrompt.Should().Be("sunset over mountains");
    }

    [Fact]
    public async Task FindByPromptsAsync_FindsExactMatch()
    {
        var entry = PromptHistory.Create("a cat on a chair", "ugly");
        await _repo.UpsertAsync(entry);

        var found = await _repo.FindByPromptsAsync("a cat on a chair", "ugly");

        found.Should().NotBeNull();
        found!.Id.Should().Be(entry.Id);
    }

    [Fact]
    public async Task FindByPromptsAsync_ReturnsNullWhenNotFound()
    {
        var found = await _repo.FindByPromptsAsync("nonexistent", "");

        found.Should().BeNull();
    }

    [Fact]
    public async Task ListRecentAsync_OrdersByMostRecent()
    {
        var entry1 = PromptHistory.Create("first prompt", "");
        await _repo.UpsertAsync(entry1);

        var entry2 = PromptHistory.Create("second prompt", "");
        await _repo.UpsertAsync(entry2);

        var results = await _repo.ListRecentAsync();

        results.Should().HaveCount(2);
        results[0].PositivePrompt.Should().Be("second prompt");
    }

    [Fact]
    public async Task SearchAsync_FindsMatchingEntries()
    {
        await _repo.UpsertAsync(PromptHistory.Create("a beautiful sunset", ""));
        await _repo.UpsertAsync(PromptHistory.Create("a cute cat", ""));
        await _repo.UpsertAsync(PromptHistory.Create("sunset over ocean", ""));

        var results = await _repo.SearchAsync("sunset");

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        var entry = PromptHistory.Create("to be deleted", "");
        await _repo.UpsertAsync(entry);

        await _repo.DeleteAsync(entry.Id);

        var all = await _repo.ListRecentAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingEntry()
    {
        var entry = PromptHistory.Create("test prompt", "negative");
        await _repo.UpsertAsync(entry);

        // Detach and re-find to simulate real usage
        _context.ChangeTracker.Clear();

        var found = await _repo.FindByPromptsAsync("test prompt", "negative");
        found!.IncrementUsage();
        await _repo.UpsertAsync(found);

        _context.ChangeTracker.Clear();
        var updated = await _repo.FindByPromptsAsync("test prompt", "negative");
        updated!.UseCount.Should().Be(2);
    }

    [Fact]
    public async Task ListRecentAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _repo.UpsertAsync(PromptHistory.Create($"prompt {i}", ""));
        }

        var results = await _repo.ListRecentAsync(take: 3);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _repo.UpsertAsync(PromptHistory.Create($"sunset variation {i}", ""));
        }

        var results = await _repo.SearchAsync("sunset", take: 5);

        results.Should().HaveCount(5);
    }

    [Fact]
    public async Task SearchAsync_MatchesExactCase()
    {
        await _repo.UpsertAsync(PromptHistory.Create("A Beautiful Sunset", ""));

        var results = await _repo.SearchAsync("Sunset");

        results.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNotThrow()
    {
        var act = () => _repo.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FindByPromptsAsync_PartialMatch_ReturnsNull()
    {
        await _repo.UpsertAsync(PromptHistory.Create("a beautiful sunset", "ugly"));

        var found = await _repo.FindByPromptsAsync("a beautiful sunset", "different negative");

        found.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
