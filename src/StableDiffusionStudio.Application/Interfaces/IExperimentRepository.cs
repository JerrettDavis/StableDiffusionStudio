using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IExperimentRepository
{
    Task<Experiment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Experiment>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Experiment experiment, CancellationToken ct = default);
    Task UpdateAsync(Experiment experiment, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ExperimentRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task UpdateRunAsync(ExperimentRun run, CancellationToken ct = default);
    Task<ExperimentRunImage?> GetRunImageByIdAsync(Guid imageId, CancellationToken ct = default);
    Task UpdateRunImageAsync(ExperimentRunImage image, CancellationToken ct = default);
}
