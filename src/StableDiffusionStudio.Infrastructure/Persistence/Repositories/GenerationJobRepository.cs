using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class GenerationJobRepository : IGenerationJobRepository
{
    private readonly AppDbContext _context;

    public GenerationJobRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GenerationJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // AsNoTracking ensures we always get fresh data from DB,
        // critical for polling job status updated by background workers
        return await _context.GenerationJobs
            .AsNoTracking()
            .Include(j => j.Images)
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<IReadOnlyList<GenerationJob>> ListByProjectAsync(
        Guid projectId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        return await _context.GenerationJobs
            .AsNoTracking()
            .Include(j => j.Images)
            .Where(j => j.ProjectId == projectId)
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddAsync(GenerationJob job, CancellationToken ct = default)
    {
        _context.GenerationJobs.Add(job);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GenerationJob job, CancellationToken ct = default)
    {
        var entry = _context.Entry(job);
        if (entry.State == EntityState.Detached)
        {
            _context.GenerationJobs.Attach(job);
            entry.State = EntityState.Modified;
        }

        // Ensure new images are tracked as Added rather than Modified
        foreach (var image in job.Images)
        {
            var imageEntry = _context.Entry(image);
            if (imageEntry.State is EntityState.Detached or EntityState.Modified)
            {
                // Check if this image actually exists in the database
                var existsInDb = await _context.GeneratedImages
                    .AsNoTracking()
                    .AnyAsync(i => i.Id == image.Id);

                if (!existsInDb)
                {
                    imageEntry.State = EntityState.Added;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
    }
}
