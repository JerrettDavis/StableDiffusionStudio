using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class PresetRepository : IPresetRepository
{
    private readonly AppDbContext _context;

    public PresetRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GenerationPresetEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.GenerationPresets.FindAsync([id], ct);
    }

    public async Task<IReadOnlyList<GenerationPresetEntity>> ListAsync(
        Guid? modelId = null, ModelFamily? family = null, CancellationToken ct = default)
    {
        var query = _context.GenerationPresets.AsNoTracking().AsQueryable();

        if (modelId.HasValue)
        {
            query = query.Where(p => p.AssociatedModelId == modelId.Value || p.AssociatedModelId == null);
        }

        if (family.HasValue)
        {
            query = query.Where(p => p.ModelFamilyFilter == family.Value || p.ModelFamilyFilter == null);
        }

        return await query
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task AddAsync(GenerationPresetEntity preset, CancellationToken ct = default)
    {
        _context.GenerationPresets.Add(preset);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(GenerationPresetEntity preset, CancellationToken ct = default)
    {
        _context.GenerationPresets.Update(preset);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var preset = await _context.GenerationPresets.FindAsync([id], ct);
        if (preset is not null)
        {
            _context.GenerationPresets.Remove(preset);
            await _context.SaveChangesAsync(ct);
        }
    }
}
