using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectCommand command, CancellationToken ct = default);
    Task RenameAsync(RenameProjectCommand command, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task PinAsync(Guid id, CancellationToken ct = default);
    Task UnpinAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectFilter filter, CancellationToken ct = default);
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
