using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IPromptHistoryRepository
{
    Task<IReadOnlyList<PromptHistory>> ListRecentAsync(int take = 50, CancellationToken ct = default);
    Task<IReadOnlyList<PromptHistory>> SearchAsync(string query, int take = 20, CancellationToken ct = default);
    Task<PromptHistory?> FindByPromptsAsync(string positivePrompt, string negativePrompt, CancellationToken ct = default);
    Task UpsertAsync(PromptHistory entry, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
