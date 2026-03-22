# Parameter Lab Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a dedicated experimentation workspace ("Parameter Lab") that lets users sweep any generation parameter across configurable ranges, view results in an interactive X/Y grid, select winners for iterative refinement, and save optimized settings as presets.

**Architecture:** New domain entities (Experiment, ExperimentRun, ExperimentRunImage) with a SweepExpander service that computes parameter combinations. Server-side ExperimentJobHandler runs as a background job using existing JobChannel infrastructure. Lab.razor page is a live viewer that reconstructs state from the database and subscribes to SignalR for real-time updates. Reuses existing ModelSelector, ParameterPanel, ImageUpload, and IInferenceBackend.

**Tech Stack:** .NET 10, Blazor Server, MudBlazor, EF Core + SQLite, SignalR, xUnit + FluentAssertions

**Spec:** `docs/specs/2026-03-21-parameter-lab-design.md`

---

## Task 1: Domain — ExperimentRunStatus Enum

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Enums/ExperimentRunStatus.cs`
- Test: `tests/StableDiffusionStudio.Domain.Tests/Enums/ExperimentRunStatusTests.cs`

- [ ] **Step 1: Create the enum**

```csharp
// src/StableDiffusionStudio.Domain/Enums/ExperimentRunStatus.cs
namespace StableDiffusionStudio.Domain.Enums;

public enum ExperimentRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
```

- [ ] **Step 2: Write a test verifying all values exist**

```csharp
// tests/StableDiffusionStudio.Domain.Tests/Enums/ExperimentRunStatusTests.cs
using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Enums;

public class ExperimentRunStatusTests
{
    [Fact]
    public void AllExpectedValues_Exist()
    {
        Enum.GetValues<ExperimentRunStatus>().Should().HaveCount(5);
        Enum.IsDefined(ExperimentRunStatus.Pending).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Running).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Completed).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Failed).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Cancelled).Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/StableDiffusionStudio.Domain.Tests --filter "ExperimentRunStatus" --no-restore -v quiet
```

- [ ] **Step 4: Commit**

```bash
git add src/StableDiffusionStudio.Domain/Enums/ExperimentRunStatus.cs tests/StableDiffusionStudio.Domain.Tests/Enums/ExperimentRunStatusTests.cs
git commit -m "feat(lab): add ExperimentRunStatus enum"
```

---

## Task 2: Domain — SweepAxis Value Object

**Files:**
- Create: `src/StableDiffusionStudio.Domain/ValueObjects/SweepAxis.cs`
- Test: `tests/StableDiffusionStudio.Domain.Tests/ValueObjects/SweepAxisTests.cs`

- [ ] **Step 1: Create the value object**

```csharp
// src/StableDiffusionStudio.Domain/ValueObjects/SweepAxis.cs
namespace StableDiffusionStudio.Domain.ValueObjects;

/// <summary>
/// Defines one axis of a parameter sweep — which parameter to vary and what values to try.
/// Values are stored as strings and parsed at sweep time based on the parameter's type.
/// </summary>
public sealed record SweepAxis
{
    /// <summary>Property name on GenerationParameters (e.g., "CfgScale", "Steps", "Sampler").</summary>
    public required string ParameterName { get; init; }

    /// <summary>Expanded values as strings (e.g., ["1.0", "1.5", "2.0"] or ["EulerA", "DDIM"]).</summary>
    public required IReadOnlyList<string> Values { get; init; }

    /// <summary>Display label override (e.g., "CFG Scale"). Falls back to ParameterName if null.</summary>
    public string? Label { get; init; }

    public string DisplayLabel => Label ?? ParameterName;

    /// <summary>
    /// Creates a numeric sweep axis from start/end/step values.
    /// </summary>
    public static SweepAxis Numeric(string parameterName, double start, double end, double step, string? label = null)
    {
        if (step <= 0) throw new ArgumentException("Step must be positive.", nameof(step));
        if (start > end) throw new ArgumentException("Start must be <= end.", nameof(start));

        var values = new List<string>();
        for (var v = start; v <= end + step / 100.0; v += step)
            values.Add(Math.Round(v, 10).ToString("G"));
        return new SweepAxis { ParameterName = parameterName, Values = values, Label = label };
    }

    /// <summary>
    /// Creates a categorical sweep axis from a list of string values.
    /// </summary>
    public static SweepAxis Categorical(string parameterName, IEnumerable<string> values, string? label = null)
    {
        var list = values.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one value is required.", nameof(values));
        return new SweepAxis { ParameterName = parameterName, Values = list, Label = label };
    }
}
```

- [ ] **Step 2: Write tests**

```csharp
// tests/StableDiffusionStudio.Domain.Tests/ValueObjects/SweepAxisTests.cs
using FluentAssertions;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class SweepAxisTests
{
    [Fact]
    public void Numeric_GeneratesCorrectValues()
    {
        var axis = SweepAxis.Numeric("CfgScale", 1.0, 3.0, 0.5);
        axis.Values.Should().BeEquivalentTo(["1", "1.5", "2", "2.5", "3"]);
        axis.ParameterName.Should().Be("CfgScale");
    }

    [Fact]
    public void Numeric_IntegerSteps_GeneratesWholeNumbers()
    {
        var axis = SweepAxis.Numeric("Steps", 5, 20, 5);
        axis.Values.Should().BeEquivalentTo(["5", "10", "15", "20"]);
    }

    [Fact]
    public void Numeric_SingleValue_WhenStartEqualsEnd()
    {
        var axis = SweepAxis.Numeric("CfgScale", 5.0, 5.0, 1.0);
        axis.Values.Should().HaveCount(1);
        axis.Values[0].Should().Be("5");
    }

    [Fact]
    public void Numeric_ZeroStep_Throws()
    {
        var act = () => SweepAxis.Numeric("CfgScale", 1.0, 5.0, 0);
        act.Should().Throw<ArgumentException>().WithMessage("*Step*positive*");
    }

    [Fact]
    public void Numeric_StartGreaterThanEnd_Throws()
    {
        var act = () => SweepAxis.Numeric("CfgScale", 10.0, 1.0, 1.0);
        act.Should().Throw<ArgumentException>().WithMessage("*Start*");
    }

    [Fact]
    public void Categorical_CreatesFromValues()
    {
        var axis = SweepAxis.Categorical("Sampler", ["EulerA", "DDIM", "DPMPlusPlus2M"]);
        axis.Values.Should().HaveCount(3);
        axis.ParameterName.Should().Be("Sampler");
    }

    [Fact]
    public void Categorical_EmptyValues_Throws()
    {
        var act = () => SweepAxis.Categorical("Sampler", []);
        act.Should().Throw<ArgumentException>().WithMessage("*At least one*");
    }

    [Fact]
    public void DisplayLabel_FallsBackToParameterName()
    {
        var axis = SweepAxis.Categorical("Sampler", ["EulerA"]);
        axis.DisplayLabel.Should().Be("Sampler");

        var labeled = SweepAxis.Categorical("Sampler", ["EulerA"], label: "Sampling Method");
        labeled.DisplayLabel.Should().Be("Sampling Method");
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/StableDiffusionStudio.Domain.Tests --filter "SweepAxis" --no-restore -v quiet
```

- [ ] **Step 4: Commit**

```bash
git add src/StableDiffusionStudio.Domain/ValueObjects/SweepAxis.cs tests/StableDiffusionStudio.Domain.Tests/ValueObjects/SweepAxisTests.cs
git commit -m "feat(lab): add SweepAxis value object with numeric and categorical factories"
```

---

## Task 3: Domain — SweepExpander Service

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Services/SweepExpander.cs`
- Test: `tests/StableDiffusionStudio.Domain.Tests/Services/SweepExpanderTests.cs`

- [ ] **Step 1: Create the sweep expander**

This service takes base GenerationParameters + sweep axes and produces the full list of parameter combinations with grid positions.

```csharp
// src/StableDiffusionStudio.Domain/Services/SweepExpander.cs
using System.Globalization;
using System.Text.Json;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Services;

/// <summary>
/// Expands sweep axes into concrete GenerationParameters combinations with grid positions.
/// </summary>
public static class SweepExpander
{
    public record SweepCombination(GenerationParameters Parameters, int GridX, int GridY, Dictionary<string, string> AxisValues);

    /// <summary>
    /// Produces the cartesian product of all axis values applied to the base parameters.
    /// Axis1 maps to columns (GridX), Axis2 maps to rows (GridY).
    /// </summary>
    public static IReadOnlyList<SweepCombination> Expand(
        GenerationParameters baseParams,
        SweepAxis? axis1,
        SweepAxis? axis2 = null)
    {
        if (axis1 is null) return [new SweepCombination(baseParams, 0, 0, new())];

        var axis1Values = axis1.Values;
        var axis2Values = axis2?.Values ?? [""];

        var results = new List<SweepCombination>();

        for (int y = 0; y < axis2Values.Count; y++)
        {
            for (int x = 0; x < axis1Values.Count; x++)
            {
                var axisValues = new Dictionary<string, string> { [axis1.ParameterName] = axis1Values[x] };
                var overridden = ApplyOverride(baseParams, axis1.ParameterName, axis1Values[x]);

                if (axis2 is not null)
                {
                    axisValues[axis2.ParameterName] = axis2Values[y];
                    overridden = ApplyOverride(overridden, axis2.ParameterName, axis2Values[y]);
                }

                results.Add(new SweepCombination(overridden, x, y, axisValues));
            }
        }

        return results;
    }

    /// <summary>
    /// Clones the parameters with one field overridden by value string.
    /// Uses JSON round-trip for clean cloning, then applies the override.
    /// </summary>
    public static GenerationParameters ApplyOverride(GenerationParameters baseParams, string parameterName, string value)
    {
        // Serialize to JSON, deserialize to mutable dictionary, override, re-serialize
        var json = JsonSerializer.Serialize(baseParams);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

        // Determine the target type from the known parameter types
        var jsonValue = ConvertToJsonElement(parameterName, value);
        dict[parameterName] = jsonValue;

        var overriddenJson = JsonSerializer.Serialize(dict);
        return JsonSerializer.Deserialize<GenerationParameters>(overriddenJson)!;
    }

    private static JsonElement ConvertToJsonElement(string parameterName, string value)
    {
        // Map parameter names to their JSON representation
        var jsonValue = parameterName switch
        {
            // Integer fields
            "Steps" or "Width" or "Height" or "BatchSize" or "ClipSkip" or "BatchCount" or "HiresSteps"
                => JsonSerializer.SerializeToElement(int.Parse(value, CultureInfo.InvariantCulture)),

            // Double fields
            "CfgScale" or "Eta" or "DenoisingStrength" or "HiresUpscaleFactor" or "HiresDenoisingStrength"
                => JsonSerializer.SerializeToElement(double.Parse(value, CultureInfo.InvariantCulture)),

            // Long fields
            "Seed" => JsonSerializer.SerializeToElement(long.Parse(value, CultureInfo.InvariantCulture)),

            // Bool fields
            "HiresFixEnabled" => JsonSerializer.SerializeToElement(bool.Parse(value)),

            // Guid fields
            "CheckpointModelId" => JsonSerializer.SerializeToElement(Guid.Parse(value)),
            "VaeModelId" => JsonSerializer.SerializeToElement(Guid.Parse(value)),

            // Enum fields — serialize as integer since GenerationParameters uses default JSON (int) for enums
            "Sampler" => JsonSerializer.SerializeToElement((int)Enum.Parse<Sampler>(value)),
            "Scheduler" => JsonSerializer.SerializeToElement((int)Enum.Parse<Scheduler>(value)),
            "Mode" => JsonSerializer.SerializeToElement((int)Enum.Parse<GenerationMode>(value)),

            // String fields
            "PositivePrompt" or "NegativePrompt" or "InitImagePath" or "MaskImagePath"
                => JsonSerializer.SerializeToElement(value),

            _ => throw new ArgumentException($"Unknown sweepable parameter: {parameterName}")
        };
        return jsonValue;
    }
}
```

- [ ] **Step 2: Write tests**

```csharp
// tests/StableDiffusionStudio.Domain.Tests/Services/SweepExpanderTests.cs
using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class SweepExpanderTests
{
    private static GenerationParameters BaseParams => new()
    {
        PositivePrompt = "test prompt",
        CheckpointModelId = Guid.NewGuid(),
        Steps = 20,
        CfgScale = 7.0,
        Width = 512,
        Height = 512
    };

    [Fact]
    public void Expand_SingleAxis_ProducesCorrectCount()
    {
        var axis = SweepAxis.Numeric("CfgScale", 1.0, 3.0, 1.0);
        var results = SweepExpander.Expand(BaseParams, axis);
        results.Should().HaveCount(3);
    }

    [Fact]
    public void Expand_SingleAxis_GridPositionsAreColumns()
    {
        var axis = SweepAxis.Numeric("Steps", 10, 30, 10);
        var results = SweepExpander.Expand(BaseParams, axis);
        results[0].GridX.Should().Be(0); results[0].GridY.Should().Be(0);
        results[1].GridX.Should().Be(1); results[1].GridY.Should().Be(0);
        results[2].GridX.Should().Be(2); results[2].GridY.Should().Be(0);
    }

    [Fact]
    public void Expand_TwoAxes_CartesianProduct()
    {
        var axis1 = SweepAxis.Numeric("CfgScale", 1.0, 3.0, 1.0); // 3 values
        var axis2 = SweepAxis.Numeric("Steps", 10, 20, 10);        // 2 values
        var results = SweepExpander.Expand(BaseParams, axis1, axis2);
        results.Should().HaveCount(6); // 3 x 2
    }

    [Fact]
    public void Expand_TwoAxes_GridPositionsAreCorrect()
    {
        var axis1 = SweepAxis.Categorical("Sampler", ["EulerA", "DDIM"]); // 2 columns
        var axis2 = SweepAxis.Numeric("CfgScale", 3.0, 5.0, 1.0);       // 3 rows
        var results = SweepExpander.Expand(BaseParams, axis1, axis2);

        // Row 0: (0,0), (1,0)
        // Row 1: (0,1), (1,1)
        // Row 2: (0,2), (1,2)
        results.Should().HaveCount(6);
        results.Should().Contain(r => r.GridX == 0 && r.GridY == 0);
        results.Should().Contain(r => r.GridX == 1 && r.GridY == 2);
    }

    [Fact]
    public void Expand_OverridesCfgScale()
    {
        var axis = SweepAxis.Numeric("CfgScale", 2.0, 4.0, 1.0);
        var results = SweepExpander.Expand(BaseParams, axis);
        results[0].Parameters.CfgScale.Should().Be(2.0);
        results[1].Parameters.CfgScale.Should().Be(3.0);
        results[2].Parameters.CfgScale.Should().Be(4.0);
    }

    [Fact]
    public void Expand_OverridesSteps()
    {
        var axis = SweepAxis.Numeric("Steps", 10, 30, 10);
        var results = SweepExpander.Expand(BaseParams, axis);
        results[0].Parameters.Steps.Should().Be(10);
        results[1].Parameters.Steps.Should().Be(20);
        results[2].Parameters.Steps.Should().Be(30);
    }

    [Fact]
    public void Expand_OverridesSampler()
    {
        var axis = SweepAxis.Categorical("Sampler", ["EulerA", "DDIM"]);
        var results = SweepExpander.Expand(BaseParams, axis);
        results[0].Parameters.Sampler.Should().Be(Sampler.EulerA);
        results[1].Parameters.Sampler.Should().Be(Sampler.DDIM);
    }

    [Fact]
    public void Expand_PreservesNonSweptParameters()
    {
        var axis = SweepAxis.Numeric("CfgScale", 1.0, 2.0, 1.0);
        var results = SweepExpander.Expand(BaseParams, axis);
        foreach (var r in results)
        {
            r.Parameters.Steps.Should().Be(BaseParams.Steps);
            r.Parameters.Width.Should().Be(BaseParams.Width);
            r.Parameters.PositivePrompt.Should().Be(BaseParams.PositivePrompt);
        }
    }

    [Fact]
    public void Expand_AxisValuesTracked()
    {
        var axis = SweepAxis.Numeric("CfgScale", 1.0, 2.0, 1.0);
        var results = SweepExpander.Expand(BaseParams, axis);
        results[0].AxisValues.Should().ContainKey("CfgScale").WhoseValue.Should().Be("1");
        results[1].AxisValues.Should().ContainKey("CfgScale").WhoseValue.Should().Be("2");
    }

    [Fact]
    public void Expand_NullAxis_ReturnsSingleCombination()
    {
        var results = SweepExpander.Expand(BaseParams, null);
        results.Should().HaveCount(1);
        results[0].Parameters.Should().Be(BaseParams);
    }

    [Fact]
    public void ApplyOverride_UnknownParameter_Throws()
    {
        var act = () => SweepExpander.ApplyOverride(BaseParams, "NonExistent", "42");
        act.Should().Throw<ArgumentException>().WithMessage("*Unknown*");
    }

    [Fact]
    public void Expand_OverridesPrompt()
    {
        var axis = SweepAxis.Categorical("PositivePrompt", ["a cat", "a dog"]);
        var results = SweepExpander.Expand(BaseParams, axis);
        results[0].Parameters.PositivePrompt.Should().Be("a cat");
        results[1].Parameters.PositivePrompt.Should().Be("a dog");
    }

    [Fact]
    public void Expand_OverridesDenoisingStrength()
    {
        var axis = SweepAxis.Numeric("DenoisingStrength", 0.3, 0.7, 0.2);
        var results = SweepExpander.Expand(BaseParams, axis);
        results[0].Parameters.DenoisingStrength.Should().BeApproximately(0.3, 0.01);
        results[1].Parameters.DenoisingStrength.Should().BeApproximately(0.5, 0.01);
        results[2].Parameters.DenoisingStrength.Should().BeApproximately(0.7, 0.01);
    }
}
```

- [ ] **Step 3: Run tests**

```bash
dotnet test tests/StableDiffusionStudio.Domain.Tests --filter "SweepExpander" --no-restore -v quiet
```

- [ ] **Step 4: Commit**

```bash
git add src/StableDiffusionStudio.Domain/Services/SweepExpander.cs tests/StableDiffusionStudio.Domain.Tests/Services/SweepExpanderTests.cs
git commit -m "feat(lab): add SweepExpander service — cartesian product with type-aware parameter override"
```

---

## Task 4: Domain — Experiment Entities

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Entities/Experiment.cs`
- Create: `src/StableDiffusionStudio.Domain/Entities/ExperimentRun.cs`
- Create: `src/StableDiffusionStudio.Domain/Entities/ExperimentRunImage.cs`
- Test: `tests/StableDiffusionStudio.Domain.Tests/Entities/ExperimentTests.cs`
- Test: `tests/StableDiffusionStudio.Domain.Tests/Entities/ExperimentRunTests.cs`

- [ ] **Step 1: Create ExperimentRunImage entity**

```csharp
// src/StableDiffusionStudio.Domain/Entities/ExperimentRunImage.cs
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class ExperimentRunImage
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public string FilePath { get; private set; } = null!;
    public long Seed { get; private set; }
    public double GenerationTimeSeconds { get; private set; }
    public string AxisValuesJson { get; private set; } = "{}";
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public bool IsWinner { get; private set; }
    public ContentRating ContentRating { get; private set; } = ContentRating.Unknown;
    public double NsfwScore { get; private set; }

    private ExperimentRunImage() { }

    public static ExperimentRunImage Create(
        Guid runId, string filePath, long seed, double generationTimeSeconds,
        string axisValuesJson, int gridX, int gridY)
    {
        return new ExperimentRunImage
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            FilePath = filePath,
            Seed = seed,
            GenerationTimeSeconds = generationTimeSeconds,
            AxisValuesJson = axisValuesJson,
            GridX = gridX,
            GridY = gridY
        };
    }

    public void MarkAsWinner() => IsWinner = true;
    public void UnmarkAsWinner() => IsWinner = false;
    public void SetContentRating(ContentRating rating, double score) { ContentRating = rating; NsfwScore = score; }
}
```

- [ ] **Step 2: Create ExperimentRun entity**

```csharp
// src/StableDiffusionStudio.Domain/Entities/ExperimentRun.cs
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class ExperimentRun
{
    public Guid Id { get; private set; }
    public Guid ExperimentId { get; private set; }
    public long FixedSeed { get; private set; }
    public bool UseFixedSeed { get; private set; } = true;
    public int TotalCombinations { get; private set; }
    public int CompletedCount { get; private set; }
    public ExperimentRunStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    private readonly List<ExperimentRunImage> _images = [];
    public IReadOnlyList<ExperimentRunImage> Images => _images.AsReadOnly();

    private ExperimentRun() { }

    public static ExperimentRun Create(Guid experimentId, int totalCombinations, long fixedSeed, bool useFixedSeed)
    {
        if (totalCombinations < 1) throw new ArgumentException("Must have at least one combination.");
        return new ExperimentRun
        {
            Id = Guid.NewGuid(),
            ExperimentId = experimentId,
            TotalCombinations = totalCombinations,
            FixedSeed = fixedSeed,
            UseFixedSeed = useFixedSeed,
            Status = ExperimentRunStatus.Pending
        };
    }

    public void Start() { Status = ExperimentRunStatus.Running; StartedAt = DateTimeOffset.UtcNow; }
    public void Complete() { Status = ExperimentRunStatus.Completed; CompletedAt = DateTimeOffset.UtcNow; }
    public void Fail(string error) { Status = ExperimentRunStatus.Failed; CompletedAt = DateTimeOffset.UtcNow; ErrorMessage = error; }
    public void Cancel() { Status = ExperimentRunStatus.Cancelled; CompletedAt = DateTimeOffset.UtcNow; }
    public void IncrementCompleted() => CompletedCount++;
    public void AddImage(ExperimentRunImage image) => _images.Add(image);
}
```

- [ ] **Step 3: Create Experiment entity**

```csharp
// src/StableDiffusionStudio.Domain/Entities/Experiment.cs
using System.Text.Json;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Entities;

public class Experiment
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string BaseParametersJson { get; private set; } = null!;
    public string? InitImagePath { get; private set; }
    public string SweepAxesJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private readonly List<ExperimentRun> _runs = [];
    public IReadOnlyList<ExperimentRun> Runs => _runs.AsReadOnly();

    private Experiment() { }

    public static Experiment Create(string name, GenerationParameters baseParameters, IReadOnlyList<SweepAxis> sweepAxes, string? initImagePath = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
        if (sweepAxes.Count == 0) throw new ArgumentException("At least one sweep axis is required.");
        if (sweepAxes.Count > 2) throw new ArgumentException("Maximum two sweep axes are supported.");

        return new Experiment
        {
            Id = Guid.NewGuid(),
            Name = name,
            BaseParametersJson = JsonSerializer.Serialize(baseParameters),
            SweepAxesJson = JsonSerializer.Serialize(sweepAxes),
            InitImagePath = initImagePath,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public GenerationParameters GetBaseParameters()
        => JsonSerializer.Deserialize<GenerationParameters>(BaseParametersJson)!;

    public IReadOnlyList<SweepAxis> GetSweepAxes()
        => JsonSerializer.Deserialize<List<SweepAxis>>(SweepAxesJson)!;

    public void UpdateName(string name) { Name = name; UpdatedAt = DateTimeOffset.UtcNow; }
    public void UpdateDescription(string? description) { Description = description; UpdatedAt = DateTimeOffset.UtcNow; }

    public void UpdateConfiguration(GenerationParameters baseParameters, IReadOnlyList<SweepAxis> sweepAxes, string? initImagePath = null)
    {
        BaseParametersJson = JsonSerializer.Serialize(baseParameters);
        SweepAxesJson = JsonSerializer.Serialize(sweepAxes);
        InitImagePath = initImagePath;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public ExperimentRun CreateRun(int totalCombinations, long fixedSeed, bool useFixedSeed)
    {
        var run = ExperimentRun.Create(Id, totalCombinations, fixedSeed, useFixedSeed);
        _runs.Add(run);
        return run;
    }
}
```

- [ ] **Step 4: Write tests for Experiment**

```csharp
// tests/StableDiffusionStudio.Domain.Tests/Entities/ExperimentTests.cs
using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ExperimentTests
{
    private static GenerationParameters BaseParams => new()
    {
        PositivePrompt = "test",
        CheckpointModelId = Guid.NewGuid(),
        Steps = 20, CfgScale = 7.0, Width = 512, Height = 512
    };

    [Fact]
    public void Create_ValidInputs_CreatesExperiment()
    {
        var axes = new List<SweepAxis> { SweepAxis.Numeric("CfgScale", 1.0, 5.0, 1.0) };
        var experiment = Experiment.Create("Test Experiment", BaseParams, axes);

        experiment.Id.Should().NotBeEmpty();
        experiment.Name.Should().Be("Test Experiment");
        experiment.GetBaseParameters().PositivePrompt.Should().Be("test");
        experiment.GetSweepAxes().Should().HaveCount(1);
        experiment.Runs.Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var axes = new List<SweepAxis> { SweepAxis.Numeric("CfgScale", 1.0, 5.0, 1.0) };
        var act = () => Experiment.Create("", BaseParams, axes);
        act.Should().Throw<ArgumentException>().WithMessage("*Name*");
    }

    [Fact]
    public void Create_NoAxes_Throws()
    {
        var act = () => Experiment.Create("Test", BaseParams, []);
        act.Should().Throw<ArgumentException>().WithMessage("*At least one*");
    }

    [Fact]
    public void Create_ThreeAxes_Throws()
    {
        var axes = new List<SweepAxis>
        {
            SweepAxis.Numeric("CfgScale", 1.0, 5.0, 1.0),
            SweepAxis.Numeric("Steps", 10, 20, 5),
            SweepAxis.Numeric("Eta", 0.0, 1.0, 0.5)
        };
        var act = () => Experiment.Create("Test", BaseParams, axes);
        act.Should().Throw<ArgumentException>().WithMessage("*Maximum two*");
    }

    [Fact]
    public void CreateRun_AddsRunToCollection()
    {
        var axes = new List<SweepAxis> { SweepAxis.Numeric("CfgScale", 1.0, 3.0, 1.0) };
        var experiment = Experiment.Create("Test", BaseParams, axes);

        var run = experiment.CreateRun(3, 12345, true);

        experiment.Runs.Should().HaveCount(1);
        run.ExperimentId.Should().Be(experiment.Id);
        run.TotalCombinations.Should().Be(3);
        run.FixedSeed.Should().Be(12345);
    }

    [Fact]
    public void UpdateConfiguration_UpdatesParametersAndAxes()
    {
        var axes = new List<SweepAxis> { SweepAxis.Numeric("CfgScale", 1.0, 5.0, 1.0) };
        var experiment = Experiment.Create("Test", BaseParams, axes);
        var originalUpdatedAt = experiment.UpdatedAt;

        var newAxes = new List<SweepAxis> { SweepAxis.Numeric("Steps", 10, 40, 10) };
        var newParams = BaseParams with { CfgScale = 4.0 };
        experiment.UpdateConfiguration(newParams, newAxes);

        experiment.GetBaseParameters().CfgScale.Should().Be(4.0);
        experiment.GetSweepAxes()[0].ParameterName.Should().Be("Steps");
        experiment.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }
}
```

- [ ] **Step 5: Write tests for ExperimentRun**

```csharp
// tests/StableDiffusionStudio.Domain.Tests/Entities/ExperimentRunTests.cs
using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ExperimentRunTests
{
    [Fact]
    public void Create_ValidInputs_SetsPending()
    {
        var run = ExperimentRun.Create(Guid.NewGuid(), 10, 12345, true);
        run.Status.Should().Be(ExperimentRunStatus.Pending);
        run.TotalCombinations.Should().Be(10);
        run.CompletedCount.Should().Be(0);
        run.UseFixedSeed.Should().BeTrue();
    }

    [Fact]
    public void Create_ZeroCombinations_Throws()
    {
        var act = () => ExperimentRun.Create(Guid.NewGuid(), 0, 12345, true);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StateTransitions_Work()
    {
        var run = ExperimentRun.Create(Guid.NewGuid(), 5, 12345, true);
        run.Start();
        run.Status.Should().Be(ExperimentRunStatus.Running);
        run.StartedAt.Should().NotBeNull();

        run.IncrementCompleted();
        run.CompletedCount.Should().Be(1);

        run.Complete();
        run.Status.Should().Be(ExperimentRunStatus.Completed);
        run.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Fail_SetsErrorMessage()
    {
        var run = ExperimentRun.Create(Guid.NewGuid(), 5, 12345, true);
        run.Start();
        run.Fail("Out of memory");
        run.Status.Should().Be(ExperimentRunStatus.Failed);
        run.ErrorMessage.Should().Be("Out of memory");
    }

    [Fact]
    public void AddImage_AddsToCollection()
    {
        var run = ExperimentRun.Create(Guid.NewGuid(), 5, 12345, true);
        var image = ExperimentRunImage.Create(run.Id, "/path/img.png", 12345, 2.5, "{}", 0, 0);
        run.AddImage(image);
        run.Images.Should().HaveCount(1);
    }

    [Fact]
    public void ExperimentRunImage_WinnerToggle()
    {
        var image = ExperimentRunImage.Create(Guid.NewGuid(), "/path/img.png", 12345, 2.5, "{}", 0, 0);
        image.IsWinner.Should().BeFalse();
        image.MarkAsWinner();
        image.IsWinner.Should().BeTrue();
        image.UnmarkAsWinner();
        image.IsWinner.Should().BeFalse();
    }
}
```

- [ ] **Step 6: Run all tests**

```bash
dotnet test tests/StableDiffusionStudio.Domain.Tests --filter "Experiment" --no-restore -v quiet
```

- [ ] **Step 7: Commit**

```bash
git add src/StableDiffusionStudio.Domain/Entities/Experiment.cs src/StableDiffusionStudio.Domain/Entities/ExperimentRun.cs src/StableDiffusionStudio.Domain/Entities/ExperimentRunImage.cs tests/StableDiffusionStudio.Domain.Tests/Entities/ExperimentTests.cs tests/StableDiffusionStudio.Domain.Tests/Entities/ExperimentRunTests.cs
git commit -m "feat(lab): add Experiment, ExperimentRun, ExperimentRunImage entities"
```

---

## Task 5: Infrastructure — EF Core Configuration & DbContext

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentConfiguration.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentRunConfiguration.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentRunImageConfiguration.cs`
- Modify: `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs`

- [ ] **Step 1: Create ExperimentConfiguration**

```csharp
// src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    public void Configure(EntityTypeBuilder<Experiment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.BaseParametersJson).HasMaxLength(10000).IsRequired();
        builder.Property(e => e.SweepAxesJson).HasMaxLength(10000).IsRequired();
        builder.Property(e => e.InitImagePath).HasMaxLength(1000);

        var dtConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(e => e.CreatedAt).HasConversion(dtConverter);
        builder.Property(e => e.UpdatedAt).HasConversion(dtConverter);

        builder.HasMany(e => e.Runs)
            .WithOne()
            .HasForeignKey(r => r.ExperimentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Runs)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Experiment.Runs))!
            .SetField("_runs");

        builder.HasIndex(e => e.CreatedAt);
    }
}
```

- [ ] **Step 2: Create ExperimentRunConfiguration**

```csharp
// src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentRunConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ExperimentRunConfiguration : IEntityTypeConfiguration<ExperimentRun>
{
    public void Configure(EntityTypeBuilder<ExperimentRun> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<string>().IsRequired();
        builder.Property(r => r.ErrorMessage).HasMaxLength(2000);

        var dtConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(r => r.StartedAt).HasConversion(new DateTimeOffsetToBinaryConverter());
        builder.Property(r => r.CompletedAt).HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.HasMany(r => r.Images)
            .WithOne()
            .HasForeignKey(i => i.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(ExperimentRun.Images))!
            .SetField("_images");

        builder.HasIndex(r => r.ExperimentId);
        builder.HasIndex(r => r.Status);
    }
}
```

- [ ] **Step 3: Create ExperimentRunImageConfiguration**

```csharp
// src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentRunImageConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Configurations;

public class ExperimentRunImageConfiguration : IEntityTypeConfiguration<ExperimentRunImage>
{
    public void Configure(EntityTypeBuilder<ExperimentRunImage> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.FilePath).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.AxisValuesJson).HasMaxLength(2000).IsRequired();
        builder.Property(i => i.ContentRating).HasConversion<string>();

        builder.HasIndex(i => i.RunId);
    }
}
```

- [ ] **Step 4: Add DbSets to AppDbContext**

Add to `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs` after the existing DbSet declarations:

```csharp
public DbSet<Experiment> Experiments => Set<Experiment>();
public DbSet<ExperimentRun> ExperimentRuns => Set<ExperimentRun>();
public DbSet<ExperimentRunImage> ExperimentRunImages => Set<ExperimentRunImage>();
```

- [ ] **Step 5: Add CREATE TABLE IF NOT EXISTS to Program.cs startup schema repair**

Add the three new tables to the schema repair block in `src/StableDiffusionStudio.Web/Program.cs` (follow the existing pattern used for other tables — find the block with `CREATE TABLE IF NOT EXISTS` and add):

```sql
CREATE TABLE IF NOT EXISTS "Experiments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Experiments" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT,
    "BaseParametersJson" TEXT NOT NULL,
    "SweepAxesJson" TEXT NOT NULL,
    "InitImagePath" TEXT,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS "ExperimentRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ExperimentRuns" PRIMARY KEY,
    "ExperimentId" TEXT NOT NULL,
    "FixedSeed" INTEGER NOT NULL,
    "UseFixedSeed" INTEGER NOT NULL,
    "TotalCombinations" INTEGER NOT NULL,
    "CompletedCount" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "ErrorMessage" TEXT,
    "StartedAt" INTEGER,
    "CompletedAt" INTEGER,
    CONSTRAINT "FK_ExperimentRuns_Experiments_ExperimentId" FOREIGN KEY ("ExperimentId") REFERENCES "Experiments" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "ExperimentRunImages" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ExperimentRunImages" PRIMARY KEY,
    "RunId" TEXT NOT NULL,
    "FilePath" TEXT NOT NULL,
    "Seed" INTEGER NOT NULL,
    "GenerationTimeSeconds" REAL NOT NULL,
    "AxisValuesJson" TEXT NOT NULL,
    "GridX" INTEGER NOT NULL,
    "GridY" INTEGER NOT NULL,
    "IsWinner" INTEGER NOT NULL,
    "ContentRating" TEXT NOT NULL,
    "NsfwScore" REAL NOT NULL,
    CONSTRAINT "FK_ExperimentRunImages_ExperimentRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "ExperimentRuns" ("Id") ON DELETE CASCADE
);
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build src/StableDiffusionStudio.Web/StableDiffusionStudio.Web.csproj --no-restore -c Release
```

- [ ] **Step 7: Commit**

```bash
git add src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentConfiguration.cs src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentRunConfiguration.cs src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ExperimentRunImageConfiguration.cs src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs src/StableDiffusionStudio.Web/Program.cs
git commit -m "feat(lab): add EF Core configurations and schema for experiment tables"
```

---

## Task 6: Application — DTOs, Repository Interface, Service Interface

**Files:**
- Create: `src/StableDiffusionStudio.Application/DTOs/ExperimentDto.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ExperimentRunDto.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ExperimentRunImageDto.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IExperimentRepository.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IExperimentService.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IExperimentNotifier.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// src/StableDiffusionStudio.Application/DTOs/ExperimentDto.cs
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.DTOs;

public record ExperimentDto(
    Guid Id, string Name, string? Description,
    GenerationParameters BaseParameters,
    IReadOnlyList<SweepAxis> SweepAxes,
    string? InitImagePath,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<ExperimentRunDto> Runs);
```

```csharp
// src/StableDiffusionStudio.Application/DTOs/ExperimentRunDto.cs
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ExperimentRunDto(
    Guid Id, Guid ExperimentId,
    long FixedSeed, bool UseFixedSeed,
    int TotalCombinations, int CompletedCount,
    ExperimentRunStatus Status, string? ErrorMessage,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    IReadOnlyList<ExperimentRunImageDto> Images);
```

```csharp
// src/StableDiffusionStudio.Application/DTOs/ExperimentRunImageDto.cs
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record ExperimentRunImageDto(
    Guid Id, Guid RunId,
    string FilePath, long Seed, double GenerationTimeSeconds,
    string AxisValuesJson, int GridX, int GridY,
    bool IsWinner,
    ContentRating ContentRating, double NsfwScore);
```

- [ ] **Step 2: Create repository interface**

```csharp
// src/StableDiffusionStudio.Application/Interfaces/IExperimentRepository.cs
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IExperimentRepository
{
    Task<Experiment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Experiment>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Experiment experiment, CancellationToken ct = default);
    Task UpdateAsync(Experiment experiment, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ExperimentRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task UpdateRunAsync(ExperimentRun run, CancellationToken ct = default);
    Task<ExperimentRunImage?> GetRunImageByIdAsync(Guid imageId, CancellationToken ct = default);
    Task UpdateRunImageAsync(ExperimentRunImage image, CancellationToken ct = default);
}
```

- [ ] **Step 3: Create experiment notifier interface**

```csharp
// src/StableDiffusionStudio.Application/Interfaces/IExperimentNotifier.cs
namespace StableDiffusionStudio.Application.Interfaces;

public interface IExperimentNotifier
{
    Task SendProgressAsync(string runId, int completedIndex, int totalCount, string axisValuesJson, string imageUrl);
    Task SendCompletedAsync(string runId);
    Task SendFailedAsync(string runId, string error);
}
```

- [ ] **Step 4: Create service interface**

```csharp
// src/StableDiffusionStudio.Application/Interfaces/IExperimentService.cs
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IExperimentService
{
    Task<ExperimentDto> CreateAsync(string name, GenerationParameters baseParams, IReadOnlyList<SweepAxis> axes, string? initImagePath = null, CancellationToken ct = default);
    Task<ExperimentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ExperimentDto>> ListAsync(CancellationToken ct = default);
    Task<ExperimentDto> CloneAsync(Guid id, string newName, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ExperimentDto> UpdateAsync(Guid id, string name, GenerationParameters baseParams, IReadOnlyList<SweepAxis> axes, string? initImagePath = null, CancellationToken ct = default);
    Task<ExperimentRunDto> StartRunAsync(Guid experimentId, long seed, bool useFixedSeed, CancellationToken ct = default);
    Task<ExperimentRunDto?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task ToggleWinnerAsync(Guid imageId, CancellationToken ct = default);
    Task<GenerationParameters> GetWinnerParametersAsync(Guid imageId, CancellationToken ct = default);
}
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/StableDiffusionStudio.Application/StableDiffusionStudio.Application.csproj --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add src/StableDiffusionStudio.Application/DTOs/ExperimentDto.cs src/StableDiffusionStudio.Application/DTOs/ExperimentRunDto.cs src/StableDiffusionStudio.Application/DTOs/ExperimentRunImageDto.cs src/StableDiffusionStudio.Application/Interfaces/IExperimentRepository.cs src/StableDiffusionStudio.Application/Interfaces/IExperimentService.cs src/StableDiffusionStudio.Application/Interfaces/IExperimentNotifier.cs
git commit -m "feat(lab): add DTOs, repository interface, service interface, notifier interface"
```

---

## Task 7: Infrastructure — Repository Implementation

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ExperimentRepository.cs`
- Test: `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/ExperimentRepositoryTests.cs`

- [ ] **Step 1: Create repository**

```csharp
// src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ExperimentRepository.cs
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class ExperimentRepository : IExperimentRepository
{
    private readonly AppDbContext _context;

    public ExperimentRepository(AppDbContext context) => _context = context;

    public async Task<Experiment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Experiments.AsNoTracking()
            .Include(e => e.Runs).ThenInclude(r => r.Images)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<Experiment>> ListAsync(CancellationToken ct = default)
        => await _context.Experiments.AsNoTracking()
            .Include(e => e.Runs)
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Experiment experiment, CancellationToken ct = default)
    {
        _context.Experiments.Add(experiment);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Experiment experiment, CancellationToken ct = default)
    {
        var entry = _context.Entry(experiment);
        if (entry.State == EntityState.Detached)
        {
            _context.Experiments.Attach(experiment);
            entry.State = EntityState.Modified;
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var experiment = await _context.Experiments.FindAsync([id], ct);
        if (experiment is not null)
        {
            _context.Experiments.Remove(experiment);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<ExperimentRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default)
        => await _context.ExperimentRuns.AsNoTracking()
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

    public async Task UpdateRunAsync(ExperimentRun run, CancellationToken ct = default)
    {
        var entry = _context.Entry(run);
        if (entry.State == EntityState.Detached)
        {
            _context.ExperimentRuns.Attach(run);
            entry.State = EntityState.Modified;
        }

        foreach (var image in run.Images)
        {
            var imageEntry = _context.Entry(image);
            if (imageEntry.State is EntityState.Detached or EntityState.Modified)
            {
                var exists = await _context.ExperimentRunImages.AsNoTracking().AnyAsync(i => i.Id == image.Id, ct);
                imageEntry.State = exists ? EntityState.Modified : EntityState.Added;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<ExperimentRunImage?> GetRunImageByIdAsync(Guid imageId, CancellationToken ct = default)
        => await _context.ExperimentRunImages.FirstOrDefaultAsync(i => i.Id == imageId, ct);

    public async Task UpdateRunImageAsync(ExperimentRunImage image, CancellationToken ct = default)
    {
        var entry = _context.Entry(image);
        if (entry.State == EntityState.Detached)
        {
            _context.ExperimentRunImages.Attach(image);
            entry.State = EntityState.Modified;
        }
        await _context.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 2: Build and run existing tests to ensure no regressions**

```bash
dotnet build --no-restore -c Release && dotnet test --filter "FullyQualifiedName!~E2E" --no-restore -v quiet
```

- [ ] **Step 3: Commit**

```bash
git add src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ExperimentRepository.cs
git commit -m "feat(lab): add ExperimentRepository implementation"
```

---

## Task 8: Application — ExperimentService & SignalR Notifier

**Files:**
- Create: `src/StableDiffusionStudio.Application/Services/ExperimentService.cs`
- Create: `src/StableDiffusionStudio.Web/Hubs/SignalRExperimentNotifier.cs`
- Modify: `src/StableDiffusionStudio.Web/Hubs/StudioHub.cs`
- Test: `tests/StableDiffusionStudio.Application.Tests/Services/ExperimentServiceTests.cs`

- [ ] **Step 1: Create ExperimentService**

```csharp
// src/StableDiffusionStudio.Application/Services/ExperimentService.cs
using System.Text.Json;
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

    public ExperimentService(IExperimentRepository repository, IJobQueue jobQueue)
    {
        _repository = repository;
        _jobQueue = jobQueue;
    }

    public async Task<ExperimentDto> CreateAsync(string name, GenerationParameters baseParams, IReadOnlyList<SweepAxis> axes, string? initImagePath = null, CancellationToken ct = default)
    {
        var experiment = Experiment.Create(name, baseParams, axes, initImagePath);
        await _repository.AddAsync(experiment, ct);
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
        var original = await _repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Experiment {id} not found.");
        var clone = Experiment.Create(newName, original.GetBaseParameters(), original.GetSweepAxes(), original.InitImagePath);
        await _repository.AddAsync(clone, ct);
        return ToDto(clone);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        => await _repository.DeleteAsync(id, ct);

    public async Task<ExperimentDto> UpdateAsync(Guid id, string name, GenerationParameters baseParams, IReadOnlyList<SweepAxis> axes, string? initImagePath = null, CancellationToken ct = default)
    {
        var experiment = await _repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Experiment {id} not found.");
        experiment.UpdateName(name);
        experiment.UpdateConfiguration(baseParams, axes, initImagePath);
        await _repository.UpdateAsync(experiment, ct);
        return ToDto(experiment);
    }

    public async Task<ExperimentRunDto> StartRunAsync(Guid experimentId, long seed, bool useFixedSeed, CancellationToken ct = default)
    {
        var experiment = await _repository.GetByIdAsync(experimentId, ct)
            ?? throw new InvalidOperationException($"Experiment {experimentId} not found.");

        var axes = experiment.GetSweepAxes();
        var combinations = SweepExpander.Expand(experiment.GetBaseParameters(), axes[0], axes.Count > 1 ? axes[1] : null);
        var run = experiment.CreateRun(combinations.Count, seed, useFixedSeed);
        await _repository.UpdateAsync(experiment, ct);

        // Enqueue as background job
        var jobData = JsonSerializer.Serialize(new ExperimentJobData(run.Id));
        await _jobQueue.EnqueueAsync("experiment", jobData, ct);

        return ToRunDto(run);
    }

    public async Task<ExperimentRunDto?> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _repository.GetRunByIdAsync(runId, ct);
        return run is null ? null : ToRunDto(run);
    }

    public async Task ToggleWinnerAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _repository.GetRunImageByIdAsync(imageId, ct)
            ?? throw new InvalidOperationException($"Image {imageId} not found.");
        if (image.IsWinner) image.UnmarkAsWinner(); else image.MarkAsWinner();
        await _repository.UpdateRunImageAsync(image, ct);
    }

    public async Task<GenerationParameters> GetWinnerParametersAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _repository.GetRunImageByIdAsync(imageId, ct)
            ?? throw new InvalidOperationException($"Image {imageId} not found.");
        var run = await _repository.GetRunByIdAsync(image.RunId, ct)
            ?? throw new InvalidOperationException($"Run {image.RunId} not found.");

        // Find the experiment to get base params
        // The run's experiment can be found through the ExperimentId FK
        var experimentId = run.ExperimentId;
        var experiment = await _repository.GetByIdAsync(experimentId, ct)
            ?? throw new InvalidOperationException($"Experiment {experimentId} not found.");

        // Apply the axis overrides from this image to the base params
        var baseParams = experiment.GetBaseParameters();
        var axisValues = JsonSerializer.Deserialize<Dictionary<string, string>>(image.AxisValuesJson) ?? new();
        var result = baseParams;
        foreach (var (paramName, paramValue) in axisValues)
            result = SweepExpander.ApplyOverride(result, paramName, paramValue);

        return result with { Seed = image.Seed };
    }

    private static ExperimentDto ToDto(Experiment e) => new(
        e.Id, e.Name, e.Description,
        e.GetBaseParameters(), e.GetSweepAxes(),
        e.InitImagePath,
        e.CreatedAt, e.UpdatedAt,
        e.Runs.Select(ToRunDto).ToList());

    private static ExperimentRunDto ToRunDto(ExperimentRun r) => new(
        r.Id, r.ExperimentId,
        r.FixedSeed, r.UseFixedSeed,
        r.TotalCombinations, r.CompletedCount,
        r.Status, r.ErrorMessage,
        r.StartedAt, r.CompletedAt,
        r.Images.Select(ToImageDto).ToList());

    private static ExperimentRunImageDto ToImageDto(ExperimentRunImage i) => new(
        i.Id, i.RunId,
        i.FilePath, i.Seed, i.GenerationTimeSeconds,
        i.AxisValuesJson, i.GridX, i.GridY,
        i.IsWinner,
        i.ContentRating, i.NsfwScore);

    internal sealed record ExperimentJobData(Guid RunId);
}
```

- [ ] **Step 2: Create SignalR notifier**

```csharp
// src/StableDiffusionStudio.Web/Hubs/SignalRExperimentNotifier.cs
using Microsoft.AspNetCore.SignalR;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Hubs;

public class SignalRExperimentNotifier : IExperimentNotifier
{
    private readonly IHubContext<StudioHub> _hubContext;

    public SignalRExperimentNotifier(IHubContext<StudioHub> hubContext) => _hubContext = hubContext;

    public async Task SendProgressAsync(string runId, int completedIndex, int totalCount, string axisValuesJson, string imageUrl)
        => await _hubContext.Clients.All.SendAsync("ExperimentProgress", runId, completedIndex, totalCount, axisValuesJson, imageUrl);

    public async Task SendCompletedAsync(string runId)
        => await _hubContext.Clients.All.SendAsync("ExperimentComplete", runId);

    public async Task SendFailedAsync(string runId, string error)
        => await _hubContext.Clients.All.SendAsync("ExperimentFailed", runId, error);
}
```

- [ ] **Step 3: Update StudioHub documentation**

Add to the comments in `src/StableDiffusionStudio.Web/Hubs/StudioHub.cs`:

```csharp
// ExperimentProgress(string runId, int completedIndex, int totalCount, string axisValuesJson, string imageUrl)
// ExperimentComplete(string runId)
// ExperimentFailed(string runId, string error)
```

- [ ] **Step 4: Build and verify**

```bash
dotnet build --no-restore -c Release
```

- [ ] **Step 5: Commit**

```bash
git add src/StableDiffusionStudio.Application/Services/ExperimentService.cs src/StableDiffusionStudio.Web/Hubs/SignalRExperimentNotifier.cs src/StableDiffusionStudio.Web/Hubs/StudioHub.cs
git commit -m "feat(lab): add ExperimentService, SignalR notifier, hub events"
```

---

## Task 9: Infrastructure — ExperimentJobHandler

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Jobs/ExperimentJobHandler.cs`

- [ ] **Step 1: Create the job handler**

This is the core engine — iterates combinations, generates images, saves results, sends SignalR updates.

```csharp
// src/StableDiffusionStudio.Infrastructure/Jobs/ExperimentJobHandler.cs
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class ExperimentJobHandler : IJobHandler
{
    private readonly IExperimentRepository _experimentRepository;
    private readonly IModelCatalogRepository _modelCatalogRepository;
    private readonly IInferenceBackend _inferenceBackend;
    private readonly IAppPaths _appPaths;
    private readonly IContentSafetyService _contentSafetyService;
    private readonly IExperimentNotifier _experimentNotifier;
    private readonly IFluxComponentResolver? _fluxResolver;
    private readonly ILogger<ExperimentJobHandler> _logger;

    public ExperimentJobHandler(
        IExperimentRepository experimentRepository,
        IModelCatalogRepository modelCatalogRepository,
        IInferenceBackend inferenceBackend,
        IAppPaths appPaths,
        IContentSafetyService contentSafetyService,
        IExperimentNotifier experimentNotifier,
        ILogger<ExperimentJobHandler> logger,
        IFluxComponentResolver? fluxResolver = null)
    {
        _experimentRepository = experimentRepository;
        _modelCatalogRepository = modelCatalogRepository;
        _inferenceBackend = inferenceBackend;
        _appPaths = appPaths;
        _contentSafetyService = contentSafetyService;
        _experimentNotifier = experimentNotifier;
        _fluxResolver = fluxResolver;
        _logger = logger;
    }

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        ExperimentService.ExperimentJobData? jobData;
        try { jobData = JsonSerializer.Deserialize<ExperimentService.ExperimentJobData>(job.Data!); }
        catch { jobData = null; }

        if (jobData is null || jobData.RunId == Guid.Empty)
        {
            job.Fail("Invalid experiment job data");
            return;
        }

        var run = await _experimentRepository.GetRunByIdAsync(jobData.RunId, ct);
        if (run is null) { job.Fail($"Experiment run {jobData.RunId} not found"); return; }

        var experiment = await _experimentRepository.GetByIdAsync(run.ExperimentId, ct);
        if (experiment is null) { job.Fail($"Experiment {run.ExperimentId} not found"); return; }

        run.Start();
        await _experimentRepository.UpdateRunAsync(run, ct);
        job.UpdateProgress(5, "Expanding sweep parameters");

        var runIdStr = run.Id.ToString();

        try
        {
            var baseParams = experiment.GetBaseParameters();
            var axes = experiment.GetSweepAxes();
            var combinations = SweepExpander.Expand(baseParams, axes[0], axes.Count > 1 ? axes[1] : null);

            // Group by model for efficient model loading
            var groups = combinations.GroupBy(c => c.Parameters.CheckpointModelId).ToList();

            var assetsDir = Path.Combine(_appPaths.AssetsDirectory, "experiments", experiment.Id.ToString(), run.Id.ToString());
            Directory.CreateDirectory(assetsDir);

            int completedIndex = 0;
            Guid? lastModelId = null;

            foreach (var group in groups)
            {
                // Load model if different from last
                if (lastModelId != group.Key)
                {
                    var checkpoint = await _modelCatalogRepository.GetByIdAsync(group.Key, ct);
                    if (checkpoint is null) { run.Fail($"Model {group.Key} not found"); await _experimentRepository.UpdateRunAsync(run, ct); return; }

                    string? vaePath = null;
                    var firstParams = group.First().Parameters;
                    if (firstParams.VaeModelId.HasValue)
                    {
                        var vae = await _modelCatalogRepository.GetByIdAsync(firstParams.VaeModelId.Value, ct);
                        vaePath = vae?.FilePath;
                    }

                    string? clipLPath = null, t5xxlPath = null;
                    var cpName = Path.GetFileName(checkpoint.FilePath).ToLowerInvariant();
                    if (cpName.Contains("flux") && _fluxResolver != null)
                    {
                        var components = await _fluxResolver.ResolveAsync(checkpoint.FilePath, ct);
                        if (components != null)
                        {
                            clipLPath = components.ClipLPath;
                            t5xxlPath = components.T5xxlPath;
                            vaePath ??= components.VaePath;
                        }
                    }

                    job.UpdateProgress(10, $"Loading model: {Path.GetFileName(checkpoint.FilePath)}");
                    await _inferenceBackend.LoadModelAsync(new ModelLoadRequest(checkpoint.FilePath, vaePath, [], clipLPath, t5xxlPath), ct);
                    lastModelId = group.Key;
                }

                foreach (var combo in group)
                {
                    ct.ThrowIfCancellationRequested();

                    var seed = run.UseFixedSeed ? run.FixedSeed : Random.Shared.NextInt64();
                    var genParams = combo.Parameters with { Seed = seed, BatchSize = 1, BatchCount = 1 };

                    // Load init image for img2img
                    byte[]? initImageBytes = null;
                    if (experiment.InitImagePath is not null && File.Exists(experiment.InitImagePath))
                        initImageBytes = await File.ReadAllBytesAsync(experiment.InitImagePath, ct);

                    var request = new InferenceRequest(
                        genParams.PositivePrompt, genParams.NegativePrompt,
                        genParams.Sampler, genParams.Scheduler,
                        genParams.Steps, genParams.CfgScale, seed,
                        genParams.Width, genParams.Height, 1, genParams.ClipSkip,
                        genParams.Eta, initImageBytes, genParams.DenoisingStrength);

                    var result = await _inferenceBackend.GenerateAsync(request,
                        new DirectProgress<InferenceProgress>(_ => { }), ct);

                    if (!result.Success || result.Images.Count == 0)
                    {
                        _logger.LogWarning("Experiment image failed at grid ({X},{Y}): {Error}", combo.GridX, combo.GridY, result.Error);
                        completedIndex++;
                        continue;
                    }

                    var imageData = result.Images[0];
                    var fileName = $"grid_{combo.GridX}_{combo.GridY}_{seed}.png";
                    var filePath = Path.Combine(assetsDir, fileName);
                    await File.WriteAllBytesAsync(filePath, imageData.ImageBytes, ct);

                    var axisValuesJson = JsonSerializer.Serialize(combo.AxisValues);
                    var runImage = ExperimentRunImage.Create(run.Id, filePath, seed, imageData.GenerationTimeSeconds, axisValuesJson, combo.GridX, combo.GridY);

                    // Content safety classification
                    try
                    {
                        var classification = await _contentSafetyService.ClassifyAsync(imageData.ImageBytes, ct);
                        runImage.SetContentRating(classification.Rating, classification.NsfwScore);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Content safety classification failed");
                    }

                    run.AddImage(runImage);
                    run.IncrementCompleted();
                    await _experimentRepository.UpdateRunAsync(run, ct);

                    completedIndex++;
                    var pct = 10 + (int)(completedIndex * 85.0 / combinations.Count);
                    job.UpdateProgress(pct, $"Image {completedIndex}/{combinations.Count}");

                    var imageUrl = _appPaths.GetImageUrl(filePath);
                    try { await _experimentNotifier.SendProgressAsync(runIdStr, completedIndex, combinations.Count, axisValuesJson, imageUrl); }
                    catch (Exception ex) { _logger.LogDebug(ex, "Failed to send experiment progress"); }
                }
            }

            run.Complete();
            await _experimentRepository.UpdateRunAsync(run, ct);
            job.UpdateProgress(100, "Complete");
            await _experimentNotifier.SendCompletedAsync(runIdStr);

            _logger.LogInformation("Experiment run {RunId} completed: {Count}/{Total} images",
                run.Id, run.CompletedCount, run.TotalCombinations);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Experiment run {RunId} failed", run.Id);
            run.Fail(ex.Message);
            await _experimentRepository.UpdateRunAsync(run, ct);
            job.Fail(ex.Message);
            await _experimentNotifier.SendFailedAsync(runIdStr, ex.Message);
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build --no-restore -c Release
```

- [ ] **Step 3: Commit**

```bash
git add src/StableDiffusionStudio.Infrastructure/Jobs/ExperimentJobHandler.cs
git commit -m "feat(lab): add ExperimentJobHandler — background job runner for sweep experiments"
```

---

## Task 10: DI Registration

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Program.cs`

- [ ] **Step 1: Add DI registrations**

Add after the existing generation service registrations in `Program.cs`:

```csharp
// Experiment / Parameter Lab
builder.Services.AddScoped<IExperimentRepository, ExperimentRepository>();
builder.Services.AddScoped<IExperimentService, ExperimentService>();
builder.Services.AddScoped<IExperimentNotifier, SignalRExperimentNotifier>();
builder.Services.AddKeyedScoped<IJobHandler, ExperimentJobHandler>("experiment");
```

Add required usings if not already present:
```csharp
using StableDiffusionStudio.Web.Hubs;
```

- [ ] **Step 2: Build and run all tests**

```bash
dotnet build --no-restore -c Release && dotnet test --filter "FullyQualifiedName!~E2E" --no-restore -v quiet
```

- [ ] **Step 3: Commit**

```bash
git add src/StableDiffusionStudio.Web/Program.cs
git commit -m "feat(lab): register experiment services in DI container"
```

---

## Task 11: Web — Lab Page, SweepAxisEditor, ExperimentGrid

**Files:**
- Create: `src/StableDiffusionStudio.Web/Components/Pages/Lab.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/SweepAxisEditor.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/ExperimentGrid.razor`
- Modify: `src/StableDiffusionStudio.Web/Components/Layout/NavMenu.razor`

This is the largest task — the full UI. The implementer should create all three components and the nav entry. Key guidance:

- [ ] **Step 1: Create SweepAxisEditor component**

This is the reusable axis configuration component. It renders differently based on the selected parameter type: numeric fields get start/end/step inputs, enum fields get multi-select chips, model fields get a model picker, string fields get a textarea.

The component should:
- Accept a `ParameterName` parameter (string, bound to a dropdown of all GenerationParameters property names)
- Accept `Values` as `List<string>` with two-way binding
- Show a parameter name dropdown listing all sweepable fields: Steps, CfgScale, DenoisingStrength, ClipSkip, Eta, Sampler, Scheduler, Seed, Width, Height, BatchSize, BatchCount, HiresFixEnabled, HiresUpscaleFactor, HiresSteps, HiresDenoisingStrength, PositivePrompt, NegativePrompt, CheckpointModelId
- When a numeric parameter is selected: show Start, End, Step numeric fields with a "Preview" chip set showing expanded values
- When an enum parameter (Sampler, Scheduler) is selected: show multi-select chips for all enum values
- When CheckpointModelId is selected: show multiple ModelSelector components
- When PositivePrompt/NegativePrompt is selected: show a textarea with one variation per line
- When a bool (HiresFixEnabled) is selected: show chips for "True" and "False"
- Show a "Clear" button to reset the axis
- Display the total count of values

- [ ] **Step 2: Create ExperimentGrid component**

This is the interactive matrix display. It should:
- Accept `ExperimentRunDto` as parameter
- Accept `IAppPaths` for image URL resolution
- Render a CSS grid with column headers (Axis 1 values) and row headers (Axis 2 values)
- Each cell: image thumbnail (square, object-fit:cover) or skeleton placeholder if not yet generated
- Currently generating cell: show progress spinner overlay
- Click image: toggle gold border + IsWinner state via `OnWinnerToggled` EventCallback
- Accept `OnWinnerToggled` EventCallback<Guid> for the image ID
- For single-axis sweeps: render as a single row
- Show axis labels from the AxisValuesJson on each cell (tooltip or small text)

- [ ] **Step 3: Create Lab.razor page**

Three-panel layout using MudGrid:

Left panel (xs=12 md=4):
- Experiment name MudTextField
- Save / Load / Clone / Delete buttons
- MudSelect to load saved experiments
- txt2img / img2img mode toggle
- ImageUpload when img2img
- ModelSelector for checkpoint
- ModelSelector for VAE (optional)
- LoraSelector
- Positive/Negative prompt fields
- ParameterPanel (collapsible, as base parameters)
- Fixed seed toggle + seed field

Middle panel (xs=12 md=2):
- "Axis 1 (columns)" — SweepAxisEditor
- "Axis 2 (rows)" — SweepAxisEditor (optional)
- Total combinations count display
- Run Experiment button (primary)
- Cancel button (during run)

Right panel (xs=12 md=6):
- ExperimentGrid component
- Action bar: Apply Winner, Save as Preset, Send to Generate, Download Grid
- Run history tabs/dropdown

The page should:
- Subscribe to SignalR events: ExperimentProgress, ExperimentComplete, ExperimentFailed
- On load, check for any in-progress runs and auto-attach
- Reconstruct grid from database for completed/partial runs
- "Apply Winner" copies the winning image's full parameters (via `GetWinnerParametersAsync`) back to the base parameter fields and clears the sweep axes
- "Save as Preset" opens the existing SavePresetDialog with winner parameters
- "Send to Generate" navigates to `/generate` with parameters

- [ ] **Step 4: Add nav menu entry**

Add to `NavMenu.razor` after the Presets link:

```razor
<MudNavLink Href="/lab" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Science">Parameter Lab</MudNavLink>
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build --no-restore -c Release
```

- [ ] **Step 6: Commit**

```bash
git add src/StableDiffusionStudio.Web/Components/Pages/Lab.razor src/StableDiffusionStudio.Web/Components/Shared/SweepAxisEditor.razor src/StableDiffusionStudio.Web/Components/Shared/ExperimentGrid.razor src/StableDiffusionStudio.Web/Components/Layout/NavMenu.razor
git commit -m "feat(lab): add Parameter Lab page with sweep editor, experiment grid, and nav entry"
```

---

## Task 12: Cross-Page Integration

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Components/Dialogs/ImageDetailDialog.razor`
- Modify: `src/StableDiffusionStudio.Web/Components/Shared/GenerationGallery.razor`

- [ ] **Step 1: Add "Send to Lab" to ImageDetailDialog**

Add a new button in the DialogActions section:

```razor
<MudButton Color="Color.Info" Variant="Variant.Outlined" OnClick="SendToLab"
           StartIcon="@Icons.Material.Filled.Science">
    Send to Lab
</MudButton>
```

Add the handler method:
```csharp
private void SendToLab() => MudDialog.Close(DialogResult.Ok("lab"));
```

- [ ] **Step 2: Handle "lab" result in GenerationGallery**

In the `OnImageClicked` method (or wherever the dialog result is handled), add a case for "lab":

When result is "lab", navigate to `/lab?fromJob={job.Id}`.

- [ ] **Step 3: Handle query parameter in Lab.razor**

In Lab.razor's `OnInitializedAsync`, check for `fromJob` query parameter:
- If present, load the GenerationJob's parameters via `IGenerationService.GetJobAsync`
- Pre-fill the base parameters with those parameters
- User can then configure sweep axes and run experiments

- [ ] **Step 4: Build and run tests**

```bash
dotnet build --no-restore -c Release && dotnet test --filter "FullyQualifiedName!~E2E" --no-restore -v quiet
```

- [ ] **Step 5: Commit**

```bash
git add src/StableDiffusionStudio.Web/Components/Dialogs/ImageDetailDialog.razor src/StableDiffusionStudio.Web/Components/Shared/GenerationGallery.razor src/StableDiffusionStudio.Web/Components/Pages/Lab.razor
git commit -m "feat(lab): add cross-page integration — Send to Lab from gallery and detail dialog"
```

---

## Task 13: Grid Export — Composite Image Download

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Services/GridCompositor.cs`

- [ ] **Step 1: Create GridCompositor**

Uses System.Drawing to stitch grid images into a single composite PNG with axis labels:

```csharp
// src/StableDiffusionStudio.Infrastructure/Services/GridCompositor.cs
using System.Drawing;
using System.Drawing.Imaging;

namespace StableDiffusionStudio.Infrastructure.Services;

#pragma warning disable CA1416 // Windows-only System.Drawing

public static class GridCompositor
{
    public static byte[] ComposeGrid(
        IReadOnlyList<(string FilePath, int GridX, int GridY)> images,
        IReadOnlyList<string> columnLabels,
        IReadOnlyList<string> rowLabels,
        int cellSize = 256, int labelHeight = 30)
    {
        var cols = columnLabels.Count;
        var rows = Math.Max(rowLabels.Count, 1);
        var hasRowLabels = rowLabels.Count > 0;
        var labelWidth = hasRowLabels ? 120 : 0;

        var totalWidth = labelWidth + cols * cellSize;
        var totalHeight = labelHeight + rows * cellSize;

        using var bitmap = new Bitmap(totalWidth, totalHeight);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.FromArgb(30, 30, 30));

        using var font = new Font("Arial", 10, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // Column labels
        for (int c = 0; c < cols; c++)
        {
            var rect = new RectangleF(labelWidth + c * cellSize, 0, cellSize, labelHeight);
            g.DrawString(columnLabels[c], font, brush, rect, sf);
        }

        // Row labels
        for (int r = 0; r < rows && hasRowLabels; r++)
        {
            var rect = new RectangleF(0, labelHeight + r * cellSize, labelWidth, cellSize);
            g.DrawString(rowLabels[r], font, brush, rect, sf);
        }

        // Images
        foreach (var (filePath, gridX, gridY) in images)
        {
            if (!File.Exists(filePath)) continue;
            try
            {
                using var img = Image.FromFile(filePath);
                var destRect = new Rectangle(labelWidth + gridX * cellSize, labelHeight + gridY * cellSize, cellSize, cellSize);
                g.DrawImage(img, destRect);
            }
            catch { /* Skip corrupt images */ }
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}

#pragma warning restore CA1416
```

- [ ] **Step 2: Wire into Lab.razor**

Add a "Download Grid" button in the action bar that:
1. Calls GridCompositor.ComposeGrid with the current run's images
2. Downloads via JS interop (same pattern as settings export — base64 + downloadFile)

- [ ] **Step 3: Build**

```bash
dotnet build --no-restore -c Release
```

- [ ] **Step 4: Commit**

```bash
git add src/StableDiffusionStudio.Infrastructure/Services/GridCompositor.cs src/StableDiffusionStudio.Web/Components/Pages/Lab.razor
git commit -m "feat(lab): add grid compositor for downloadable composite PNG export"
```

---

## Task 14: E2E Test & Final Verification

**Files:**
- Modify: `tests/StableDiffusionStudio.E2E.Tests/Features/FullWorkflow.feature`
- Modify: `tests/StableDiffusionStudio.E2E.Tests/Steps/FullWorkflowSteps.cs` (if new steps needed)

- [ ] **Step 1: Add E2E scenario for Lab page**

```gherkin
Scenario: Parameter Lab page loads with sweep configuration
    Given I am on the home page
    When I navigate to the parameter lab page
    Then I should see the "Parameter Lab" heading
    And the page should not have any error messages
```

Add the navigation step if not already generic:
```csharp
[When(@"I navigate to the parameter lab page")]
public async Task WhenINavigateToTheParameterLabPage()
{
    await Page.Locator(".mud-nav-link", new() { HasText = "Parameter Lab" }).ClickAsync();
    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    await Task.Delay(1000);
}
```

- [ ] **Step 2: Run all tests**

```bash
dotnet test --filter "FullyQualifiedName!~E2E" --no-restore -v quiet
dotnet build --no-restore -c Release
```

- [ ] **Step 3: Commit and push**

```bash
git add -A
git commit -m "feat(lab): add E2E test for Parameter Lab page"
git push origin main
```
