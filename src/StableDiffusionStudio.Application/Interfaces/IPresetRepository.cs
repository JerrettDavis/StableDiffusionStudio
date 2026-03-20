using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IPresetRepository
{
    Task<GenerationPresetEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GenerationPresetEntity>> ListAsync(Guid? modelId = null, ModelFamily? family = null, CancellationToken ct = default);
    Task AddAsync(GenerationPresetEntity preset, CancellationToken ct = default);
    Task UpdateAsync(GenerationPresetEntity preset, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
