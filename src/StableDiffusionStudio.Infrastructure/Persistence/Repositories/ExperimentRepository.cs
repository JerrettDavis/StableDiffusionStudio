using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class ExperimentRepository : IExperimentRepository
{
    private readonly AppDbContext _context;

    public ExperimentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Experiment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Experiments
            .AsNoTracking()
            .Include(e => e.Runs)
            .ThenInclude(r => r.Images)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<Experiment>> ListAsync(CancellationToken ct = default)
    {
        return await _context.Experiments
            .AsNoTracking()
            .Include(e => e.Runs)
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Experiment experiment, CancellationToken ct = default)
    {
        _context.Experiments.Add(experiment);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Experiment experiment, CancellationToken ct = default)
    {
        // Clear any previously tracked instance with the same key to avoid conflicts
        // (common when reading with AsNoTracking then updating in the same scope)
        var tracked = _context.ChangeTracker.Entries<Experiment>()
            .FirstOrDefault(e => e.Entity.Id == experiment.Id);
        if (tracked is not null && tracked.Entity != experiment)
            tracked.State = EntityState.Detached;

        var entry = _context.Entry(experiment);
        if (entry.State == EntityState.Detached)
        {
            _context.Experiments.Attach(experiment);
            entry.State = EntityState.Modified;
        }

        // Handle child runs that may be new.
        // When we Attach the parent experiment, EF Core recursively attaches all
        // child entities as Unchanged. New runs that don't exist in the DB yet
        // must be re-marked as Added, otherwise they silently don't get inserted.
        foreach (var run in experiment.Runs)
        {
            var runEntry = _context.Entry(run);
            var runExists = await _context.ExperimentRuns.AsNoTracking().AnyAsync(r => r.Id == run.Id, ct);
            if (!runExists)
            {
                runEntry.State = EntityState.Added;
            }
            else if (runEntry.State is EntityState.Unchanged)
            {
                runEntry.State = EntityState.Modified;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var experiment = await _context.Experiments.FindAsync([id], ct);
        if (experiment is not null)
        {
            _context.Experiments.Remove(experiment);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<ExperimentRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default)
    {
        return await _context.ExperimentRuns
            .AsNoTracking()
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    public async Task UpdateRunAsync(ExperimentRun run, CancellationToken ct = default)
    {
        var tracked = _context.ChangeTracker.Entries<ExperimentRun>()
            .FirstOrDefault(e => e.Entity.Id == run.Id);
        if (tracked is not null && tracked.Entity != run)
            tracked.State = EntityState.Detached;

        var entry = _context.Entry(run);
        if (entry.State == EntityState.Detached)
        {
            _context.ExperimentRuns.Attach(run);
            entry.State = EntityState.Modified;
        }

        // Ensure new images are tracked as Added rather than Modified.
        // Attach sets children to Unchanged — new images must be re-marked Added.
        foreach (var image in run.Images)
        {
            var imageEntry = _context.Entry(image);
            var existsInDb = await _context.ExperimentRunImages
                .AsNoTracking()
                .AnyAsync(i => i.Id == image.Id, ct);

            if (!existsInDb)
            {
                imageEntry.State = EntityState.Added;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<ExperimentRunImage?> GetRunImageByIdAsync(Guid imageId, CancellationToken ct = default)
    {
        // No AsNoTracking — tracking is needed for subsequent updates
        return await _context.ExperimentRunImages
            .FirstOrDefaultAsync(i => i.Id == imageId, ct);
    }

    public async Task UpdateRunImageAsync(ExperimentRunImage image, CancellationToken ct = default)
    {
        var entry = _context.Entry(image);
        if (entry.State == EntityState.Detached)
        {
            _context.ExperimentRunImages.Attach(image);
            entry.State = EntityState.Modified;
        }

        await _context.SaveChangesAsync(ct);
    }
}
