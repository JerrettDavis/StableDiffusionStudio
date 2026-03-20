using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Tests.Services;

public class PromptHistoryServiceTests
{
    private readonly IPromptHistoryRepository _repo = Substitute.For<IPromptHistoryRepository>();
    private readonly PromptHistoryService _service;

    public PromptHistoryServiceTests()
    {
        _service = new PromptHistoryService(_repo);
    }

    [Fact]
    public async Task RecordUsageAsync_NewPrompt_CreatesNewEntry()
    {
        _repo.FindByPromptsAsync("a sunset", "ugly", Arg.Any<CancellationToken>())
            .Returns((PromptHistory?)null);

        await _service.RecordUsageAsync("a sunset", "ugly");

        await _repo.Received(1).UpsertAsync(
            Arg.Is<PromptHistory>(e => e.PositivePrompt == "a sunset" && e.NegativePrompt == "ugly"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordUsageAsync_ExistingPrompt_IncrementsUsage()
    {
        var existing = PromptHistory.Create("a sunset", "ugly");
        _repo.FindByPromptsAsync("a sunset", "ugly", Arg.Any<CancellationToken>())
            .Returns(existing);

        await _service.RecordUsageAsync("a sunset", "ugly");

        existing.UseCount.Should().Be(2);
        await _repo.Received(1).UpsertAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordUsageAsync_EmptyPrompt_DoesNothing()
    {
        await _service.RecordUsageAsync("", "negative");

        await _repo.DidNotReceive().FindByPromptsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().UpsertAsync(Arg.Any<PromptHistory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordUsageAsync_WhitespacePrompt_DoesNothing()
    {
        await _service.RecordUsageAsync("   ", "negative");

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<PromptHistory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListRecentAsync_DelegatesToRepository()
    {
        var entries = new List<PromptHistory>
        {
            PromptHistory.Create("prompt1", ""),
            PromptHistory.Create("prompt2", "")
        };
        _repo.ListRecentAsync(50, Arg.Any<CancellationToken>()).Returns(entries);

        var result = await _service.ListRecentAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_DelegatesToRepository()
    {
        var entries = new List<PromptHistory> { PromptHistory.Create("sunset prompt", "") };
        _repo.SearchAsync("sunset", 20, Arg.Any<CancellationToken>()).Returns(entries);

        var result = await _service.SearchAsync("sunset");

        result.Should().HaveCount(1);
        result[0].PositivePrompt.Should().Be("sunset prompt");
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        await _service.DeleteAsync(id);
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }
}
