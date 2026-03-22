using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Services;

public class ExperimentService : IExperimentService
{
    private readonly IExperimentRepository _repository;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<ExperimentService>? _logger;

    public ExperimentService(
        IExperimentRepository repository,
        IJobQueue jobQueue,
        ILogger<ExperimentService>? logger = null)
    {
        _repository = repository;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task<ExperimentDto> CreateAsync(
        string name,
        GenerationParameters baseParams,
        IReadOnlyList<SweepAxis> axes,
        string? initImagePath = null,
        CancellationToken ct = default)
    {
        var experiment = Experiment.Create(name, baseParams, axes, initImagePath);
        await _repository.AddAsync(experiment, ct);
        _logger?.LogInformation("Created experiment {ExperimentId} with name '{Name}'", experiment.Id, experiment.Name);
        return ToDto(experiment);
    }

    public async Task<ExperimentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var experiment = await _repository.GetByIdAsync(id, ct);
        return experiment is null ? null : ToDto(experiment);
    }

    public async Task<IReadOnlyList<ExperimentDto>> ListAsync(CancellationToken ct = default)
    {
        var experiments = await _repository.ListAsync(ct);
        return experiments.Select(ToDto).ToList();
    }

    public async Task<ExperimentDto> CloneAsync(Guid id, string newName, CancellationToken ct = default)
    {
        var original = await _repository.GetByIdAsync(id, ct);
        if (original is null)
            throw new KeyNotFoundException($"Experiment {id} not found.");

        var clone = Experiment.Create(
            newName,
            original.GetBaseParameters(),
            original.GetSweepAxes(),
            original.InitImagePath);

        await _repository.AddAsync(clone, ct);
        _logger?.LogInformation("Cloned experiment {SourceId} into {CloneId} as '{Name}'", id, clone.Id, newName);
        return ToDto(clone);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        _logger?.LogInformation("Deleted experiment {ExperimentId}", id);
    }

    public async Task<ExperimentDto> UpdateAsync(
        Guid id,
        string name,
        GenerationParameters baseParams,
        IReadOnlyList<SweepAxis> axes,
        string? initImagePath = null,
        CancellationToken ct = default)
    {
        var experiment = await _repository.GetByIdAsync(id, ct);
        if (experiment is null)
            throw new KeyNotFoundException($"Experiment {id} not found.");

        experiment.UpdateName(name);
        experiment.UpdateConfiguration(baseParams, axes, initImagePath);
        await _repository.UpdateAsync(experiment, ct);
        _logger?.LogInformation("Updated experiment {ExperimentId}", id);
        return ToDto(experiment);
    }

    public async Task<ExperimentRunDto> StartRunAsync(
        Guid experimentId,
        long seed,
        bool useFixedSeed,
        CancellationToken ct = default)
    {
        var experiment = await _repository.GetByIdAsync(experimentId, ct);
        if (experiment is null)
            throw new KeyNotFoundException($"Experiment {experimentId} not found.");

        var baseParams = experiment.GetBaseParameters();
        var axes = experiment.GetSweepAxes();

        var axis1 = axes.Count >= 1 ? axes[0] : null;
        var axis2 = axes.Count >= 2 ? axes[1] : null;
        var combinations = SweepExpander.Expand(baseParams, axis1, axis2);

        var run = experiment.CreateRun(combinations.Count, seed, useFixedSeed);
        await _repository.UpdateAsync(experiment, ct);

        var jobData = JsonSerializer.Serialize(new ExperimentJobData(run.Id));
        await _jobQueue.EnqueueAsync("experiment", jobData, ct);

        _logger?.LogInformation(
            "Started experiment run {RunId} for experiment {ExperimentId} with {Count} combinations",
            run.Id, experimentId, combinations.Count);

        return ToRunDto(run);
    }

    public async Task<ExperimentRunDto?> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _repository.GetRunByIdAsync(runId, ct);
        return run is null ? null : ToRunDto(run);
    }

    public async Task ToggleWinnerAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _repository.GetRunImageByIdAsync(imageId, ct);
        if (image is null)
            throw new KeyNotFoundException($"Experiment run image {imageId} not found.");

        if (image.IsWinner)
            image.UnmarkAsWinner();
        else
            image.MarkAsWinner();

        await _repository.UpdateRunImageAsync(image, ct);
        _logger?.LogInformation("Toggled winner for image {ImageId}, now {IsWinner}", imageId, image.IsWinner);
    }

    public async Task<GenerationParameters> GetWinnerParametersAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _repository.GetRunImageByIdAsync(imageId, ct);
        if (image is null)
            throw new KeyNotFoundException($"Experiment run image {imageId} not found.");

        var run = await _repository.GetRunByIdAsync(image.RunId, ct);
        if (run is null)
            throw new KeyNotFoundException($"Experiment run {image.RunId} not found.");

        var experiment = await _repository.GetByIdAsync(run.ExperimentId, ct);
        if (experiment is null)
            throw new KeyNotFoundException($"Experiment {run.ExperimentId} not found.");

        var baseParams = experiment.GetBaseParameters();

        // Apply each axis override recorded on this image
        var axisValues = JsonSerializer.Deserialize<Dictionary<string, string>>(image.AxisValuesJson)
                         ?? new Dictionary<string, string>();

        var result = baseParams;
        foreach (var (paramName, paramValue) in axisValues)
            result = SweepExpander.ApplyOverride(result, paramName, paramValue);

        // Override the seed with the actual seed used to generate this image
        result = result with { Seed = image.Seed };

        return result;
    }

    // ── Mapper helpers ────────────────────────────────────────────────────────

    private static ExperimentDto ToDto(Experiment e) =>
        new(e.Id, e.Name, e.Description,
            e.GetBaseParameters(),
            e.GetSweepAxes(),
            e.InitImagePath,
            e.CreatedAt, e.UpdatedAt,
            e.Runs.Select(ToRunDto).ToList());

    private static ExperimentRunDto ToRunDto(ExperimentRun r) =>
        new(r.Id, r.ExperimentId,
            r.FixedSeed, r.UseFixedSeed,
            r.TotalCombinations, r.CompletedCount,
            r.Status, r.ErrorMessage,
            r.StartedAt, r.CompletedAt,
            r.Images.Select(ToImageDto).ToList());

    private static ExperimentRunImageDto ToImageDto(ExperimentRunImage i) =>
        new(i.Id, i.RunId,
            i.FilePath, i.Seed, i.GenerationTimeSeconds,
            i.AxisValuesJson, i.GridX, i.GridY,
            i.IsWinner,
            i.ContentRating, i.NsfwScore);
}

// ── Internal job data carrier ─────────────────────────────────────────────────

internal sealed record ExperimentJobData(Guid RunId);
