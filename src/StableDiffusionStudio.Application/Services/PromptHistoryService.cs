using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Services;

public class PromptHistoryService
{
    private readonly IPromptHistoryRepository _repository;
    private readonly ILogger<PromptHistoryService>? _logger;

    public PromptHistoryService(IPromptHistoryRepository repository, ILogger<PromptHistoryService>? logger = null)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task RecordUsageAsync(string positivePrompt, string negativePrompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(positivePrompt))
            return;

        var existing = await _repository.FindByPromptsAsync(positivePrompt, negativePrompt, ct);
        if (existing is not null)
        {
            existing.IncrementUsage();
            await _repository.UpsertAsync(existing, ct);
            _logger?.LogDebug("Incremented prompt history usage for {Id}", existing.Id);
        }
        else
        {
            var entry = PromptHistory.Create(positivePrompt, negativePrompt);
            await _repository.UpsertAsync(entry, ct);
            _logger?.LogDebug("Created new prompt history entry {Id}", entry.Id);
        }
    }

    public async Task<IReadOnlyList<PromptHistory>> ListRecentAsync(int take = 50, CancellationToken ct = default)
    {
        return await _repository.ListRecentAsync(take, ct);
    }

    public async Task<IReadOnlyList<PromptHistory>> SearchAsync(string query, CancellationToken ct = default)
    {
        return await _repository.SearchAsync(query, 20, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }
}
