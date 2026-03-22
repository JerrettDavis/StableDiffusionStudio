using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IExperimentService
{
    Task<ExperimentDto> CreateAsync(string name, GenerationParameters baseParams, IReadOnlyList<SweepAxis> axes, string? initImagePath = null, CancellationToken ct = default);
    Task<ExperimentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExperimentDto>> ListAsync(CancellationToken ct = default);
    Task<ExperimentDto> CloneAsync(Guid id, string newName, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ExperimentDto> UpdateAsync(Guid id, string name, GenerationParameters baseParams, IReadOnlyList<SweepAxis> axes, string? initImagePath = null, CancellationToken ct = default);
    Task<ExperimentRunDto> StartRunAsync(Guid experimentId, long seed, bool useFixedSeed, CancellationToken ct = default);
    Task<ExperimentRunDto?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task ToggleWinnerAsync(Guid imageId, CancellationToken ct = default);
    Task<GenerationParameters> GetWinnerParametersAsync(Guid imageId, CancellationToken ct = default);
}
