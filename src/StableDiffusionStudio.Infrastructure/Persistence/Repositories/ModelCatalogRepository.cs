using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class ModelCatalogRepository : IModelCatalogRepository
{
    private readonly AppDbContext _context;

    public ModelCatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ModelRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ModelRecords.FindAsync([id], ct);

    public async Task<IReadOnlyList<ModelRecord>> ListAsync(ModelFilter filter, CancellationToken ct = default)
    {
        var query = _context.ModelRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            query = query.Where(m => m.Title.Contains(filter.SearchTerm));
        if (filter.Family.HasValue)
            query = query.Where(m => m.ModelFamily == filter.Family.Value);
        if (filter.Format.HasValue)
            query = query.Where(m => m.Format == filter.Format.Value);
        if (filter.Status.HasValue)
            query = query.Where(m => m.Status == filter.Status.Value);
        if (!string.IsNullOrWhiteSpace(filter.Source))
            query = query.Where(m => m.Source == filter.Source);
        if (filter.Type.HasValue)
            query = query.Where(m => m.Type == filter.Type.Value);
        if (filter.ExcludeNsfw == true)
            query = query.Where(m => !m.IsNsfw);

        return await query.OrderBy(m => m.Title).Skip(filter.Skip).Take(filter.Take).ToListAsync(ct);
    }

    public async Task<ModelRecord?> GetByFilePathAsync(string filePath, CancellationToken ct = default)
        => await _context.ModelRecords.FirstOrDefaultAsync(m => m.FilePath == filePath, ct);

    public async Task UpsertAsync(ModelRecord record, CancellationToken ct = default)
    {
        var existing = await _context.ModelRecords.FindAsync([record.Id], ct);
        if (existing is null)
            _context.ModelRecords.Add(record);
        else
            _context.Entry(existing).CurrentValues.SetValues(record);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var record = await _context.ModelRecords.FindAsync([id], ct);
        if (record is not null)
        {
            _context.ModelRecords.Remove(record);
            await _context.SaveChangesAsync(ct);
        }
    }
}
