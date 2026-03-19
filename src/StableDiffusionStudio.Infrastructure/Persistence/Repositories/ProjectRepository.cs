using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Projects.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<Project>> ListAsync(ProjectFilter filter, CancellationToken ct = default)
    {
        var query = _context.Projects.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            query = query.Where(p => p.Name.Contains(filter.SearchTerm));

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.IsPinned.HasValue)
            query = query.Where(p => p.IsPinned == filter.IsPinned.Value);

        return await query
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.UpdatedAt)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Project project, CancellationToken ct = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _context.Projects.FindAsync([id], ct);
        if (project is not null)
        {
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(ct);
        }
    }
}
