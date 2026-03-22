using System.Text.Json;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Entities;

public class Experiment
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string BaseParametersJson { get; private set; } = string.Empty;
    public string? InitImagePath { get; private set; }
    public string SweepAxesJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private readonly List<ExperimentRun> _runs = [];
    public IReadOnlyList<ExperimentRun> Runs => _runs.AsReadOnly();

    private Experiment() { } // EF Core

    public static Experiment Create(
        string name,
        GenerationParameters baseParameters,
        IReadOnlyList<SweepAxis> sweepAxes,
        string? initImagePath = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Experiment name is required.", nameof(name));
        if (sweepAxes == null || sweepAxes.Count == 0)
            throw new ArgumentException("At least one sweep axis is required.", nameof(sweepAxes));
        if (sweepAxes.Count > 2)
            throw new ArgumentException("A maximum of two sweep axes are supported.", nameof(sweepAxes));

        var now = DateTimeOffset.UtcNow;
        return new Experiment
        {
            Id = Guid.NewGuid(),
            Name = name,
            BaseParametersJson = JsonSerializer.Serialize(baseParameters),
            SweepAxesJson = JsonSerializer.Serialize(sweepAxes),
            InitImagePath = initImagePath,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public GenerationParameters GetBaseParameters() =>
        JsonSerializer.Deserialize<GenerationParameters>(BaseParametersJson)!;

    public IReadOnlyList<SweepAxis> GetSweepAxes() =>
        JsonSerializer.Deserialize<IReadOnlyList<SweepAxis>>(SweepAxesJson)!;

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Experiment name is required.", nameof(name));
        Name = name;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateConfiguration(
        GenerationParameters baseParams,
        IReadOnlyList<SweepAxis> sweepAxes,
        string? initImagePath = null)
    {
        if (sweepAxes == null || sweepAxes.Count == 0)
            throw new ArgumentException("At least one sweep axis is required.", nameof(sweepAxes));
        if (sweepAxes.Count > 2)
            throw new ArgumentException("A maximum of two sweep axes are supported.", nameof(sweepAxes));

        BaseParametersJson = JsonSerializer.Serialize(baseParams);
        SweepAxesJson = JsonSerializer.Serialize(sweepAxes);
        InitImagePath = initImagePath;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public ExperimentRun CreateRun(int totalCombinations, long fixedSeed, bool useFixedSeed = true)
    {
        var run = ExperimentRun.Create(Id, totalCombinations, fixedSeed, useFixedSeed);
        _runs.Add(run);
        return run;
    }
}
