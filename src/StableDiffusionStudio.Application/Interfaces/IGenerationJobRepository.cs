using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IGenerationJobRepository
{
    Task<GenerationJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GenerationJob>> ListByProjectAsync(Guid projectId, int skip = 0, int take = 20, CancellationToken ct = default);
    Task AddAsync(GenerationJob job, CancellationToken ct = default);
    Task UpdateAsync(GenerationJob job, CancellationToken ct = default);
}
