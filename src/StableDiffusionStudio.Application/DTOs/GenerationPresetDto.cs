using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record GenerationPresetDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? AssociatedModelId,
    ModelFamily? ModelFamilyFilter,
    bool IsDefault,
    string? PositivePromptTemplate,
    string NegativePrompt,
    Sampler Sampler,
    Scheduler Scheduler,
    int Steps,
    double CfgScale,
    int Width,
    int Height,
    int BatchSize,
    int ClipSkip,
    PresetApplyMode ApplyMode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
