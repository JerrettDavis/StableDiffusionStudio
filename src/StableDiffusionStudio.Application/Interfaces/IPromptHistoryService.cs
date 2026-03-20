using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IPromptHistoryService
{
    Task RecordUsageAsync(string positivePrompt, string negativePrompt, CancellationToken ct = default);
    Task<IReadOnlyList<PromptHistory>> ListRecentAsync(int take = 50, CancellationToken ct = default);
    Task<IReadOnlyList<PromptHistory>> SearchAsync(string query, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
