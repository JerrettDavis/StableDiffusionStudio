using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IGenerationService
{
    Task<GenerationJobDto> CreateAsync(CreateGenerationCommand command, CancellationToken ct = default);
    Task<GenerationJobDto?> GetJobAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GenerationJobDto>> ListJobsForProjectAsync(Guid projectId, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<GenerationStatusDto?> GetJobStatusAsync(Guid generationJobId, CancellationToken ct = default);
    Task<GenerationParameters> CloneParametersAsync(Guid jobId, CancellationToken ct = default);
    Task ToggleFavoriteAsync(Guid imageId, CancellationToken ct = default);
    Task CancelGenerationAsync(Guid jobId, CancellationToken ct = default);
}
