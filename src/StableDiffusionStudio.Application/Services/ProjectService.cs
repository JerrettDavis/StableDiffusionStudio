using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;

    public ProjectService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectCommand command, CancellationToken ct = default)
    {
        var project = Project.Create(command.Name, command.Description);
        await _repository.AddAsync(project, ct);
        return ToDto(project);
    }

    public async Task RenameAsync(RenameProjectCommand command, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(command.Id, ct);
        project.Rename(command.NewName);
        await _repository.UpdateAsync(project, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Archive();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Restore();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task PinAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Pin();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task UnpinAsync(Guid id, CancellationToken ct = default)
    {
        var project = await GetOrThrowAsync(id, ct);
        project.Unpin();
        await _repository.UpdateAsync(project, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        var projects = await _repository.ListAsync(filter, ct);
        return projects.Select(ToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        return project is null ? null : ToDto(project);
    }

    private async Task<Project> GetOrThrowAsync(Guid id, CancellationToken ct)
    {
        var project = await _repository.GetByIdAsync(id, ct);
        return project ?? throw new KeyNotFoundException($"Project {id} not found.");
    }

    private static ProjectDto ToDto(Project p) =>
        new(p.Id, p.Name, p.Description, p.Status, p.IsPinned, p.CreatedAt, p.UpdatedAt);
}
