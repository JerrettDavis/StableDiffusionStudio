using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IPresetService
{
    Task<GenerationPresetDto> SavePresetAsync(SavePresetCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<GenerationPresetDto>> ListPresetsAsync(Guid? modelId = null, ModelFamily? family = null, CancellationToken ct = default);
    Task DeletePresetAsync(Guid id, CancellationToken ct = default);
    Task<GenerationPresetDto?> GetPresetAsync(Guid id, CancellationToken ct = default);
    Task<GenerationPresetDto?> ApplyPresetToParameters(Guid presetId, CancellationToken ct = default);
}
