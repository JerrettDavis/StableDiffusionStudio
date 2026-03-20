using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class PromptHistoryRepository : IPromptHistoryRepository
{
    private readonly AppDbContext _context;

    public PromptHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PromptHistory>> ListRecentAsync(int take = 50, CancellationToken ct = default)
    {
        return await _context.PromptHistories
            .AsNoTracking()
            .OrderByDescending(p => p.UsedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PromptHistory>> SearchAsync(string query, int take = 20, CancellationToken ct = default)
    {
        return await _context.PromptHistories
            .AsNoTracking()
            .Where(p => p.PositivePrompt.Contains(query) || p.NegativePrompt.Contains(query))
            .OrderByDescending(p => p.UsedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<PromptHistory?> FindByPromptsAsync(string positivePrompt, string negativePrompt, CancellationToken ct = default)
    {
        return await _context.PromptHistories
            .FirstOrDefaultAsync(p => p.PositivePrompt == positivePrompt && p.NegativePrompt == negativePrompt, ct);
    }

    public async Task UpsertAsync(PromptHistory entry, CancellationToken ct = default)
    {
        var existingEntry = _context.Entry(entry);
        if (existingEntry.State == EntityState.Detached)
        {
            var exists = await _context.PromptHistories.AnyAsync(p => p.Id == entry.Id, ct);
            if (exists)
            {
                _context.PromptHistories.Attach(entry);
                existingEntry.State = EntityState.Modified;
            }
            else
            {
                _context.PromptHistories.Add(entry);
            }
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.PromptHistories.FindAsync([id], ct);
        if (entry is not null)
        {
            _context.PromptHistories.Remove(entry);
            await _context.SaveChangesAsync(ct);
        }
    }
}
