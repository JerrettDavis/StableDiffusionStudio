using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.DTOs;

public record ExperimentDto(
    Guid Id, string Name, string? Description,
    GenerationParameters BaseParameters,
    IReadOnlyList<SweepAxis> SweepAxes,
    string? InitImagePath,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<ExperimentRunDto> Runs);
