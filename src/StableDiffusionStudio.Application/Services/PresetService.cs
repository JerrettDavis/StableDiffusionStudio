using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Services;

public class PresetService
{
    private readonly IPresetRepository _repository;

    public PresetService(IPresetRepository repository)
    {
        _repository = repository;
    }

    public async Task<GenerationPresetDto> SavePresetAsync(SavePresetCommand command, CancellationToken ct = default)
    {
        if (command.Id.HasValue)
        {
            var existing = await _repository.GetByIdAsync(command.Id.Value, ct);
            if (existing is not null)
            {
                existing.Update(
                    command.Name, command.Description,
                    command.AssociatedModelId, command.ModelFamilyFilter,
                    command.PositivePromptTemplate, command.NegativePrompt,
                    command.Sampler, command.Scheduler,
                    command.Steps, command.CfgScale, command.Width, command.Height,
                    command.BatchSize, command.ClipSkip);
                existing.SetDefault(command.IsDefault);
                await _repository.UpdateAsync(existing, ct);
                return ToDto(existing);
            }
        }

        var preset = GenerationPresetEntity.Create(
            command.Name, command.Description,
            command.AssociatedModelId, command.ModelFamilyFilter,
            command.PositivePromptTemplate, command.NegativePrompt,
            command.Sampler, command.Scheduler,
            command.Steps, command.CfgScale, command.Width, command.Height,
            command.BatchSize, command.ClipSkip);
        preset.SetDefault(command.IsDefault);
        await _repository.AddAsync(preset, ct);
        return ToDto(preset);
    }

    public async Task<IReadOnlyList<GenerationPresetDto>> ListPresetsAsync(
        Guid? modelId = null, ModelFamily? family = null, CancellationToken ct = default)
    {
        var presets = await _repository.ListAsync(modelId, family, ct);
        return presets.Select(ToDto).ToList();
    }

    public async Task DeletePresetAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }

    public async Task<GenerationPresetDto?> GetPresetAsync(Guid id, CancellationToken ct = default)
    {
        var preset = await _repository.GetByIdAsync(id, ct);
        return preset is null ? null : ToDto(preset);
    }

    public async Task<GenerationPresetDto?> ApplyPresetToParameters(Guid presetId, CancellationToken ct = default)
    {
        var preset = await _repository.GetByIdAsync(presetId, ct);
        return preset is null ? null : ToDto(preset);
    }

    private static GenerationPresetDto ToDto(GenerationPresetEntity p) =>
        new(p.Id, p.Name, p.Description,
            p.AssociatedModelId, p.ModelFamilyFilter, p.IsDefault,
            p.PositivePromptTemplate, p.NegativePrompt,
            p.Sampler, p.Scheduler, p.Steps, p.CfgScale,
            p.Width, p.Height, p.BatchSize, p.ClipSkip,
            p.CreatedAt, p.UpdatedAt);
}
