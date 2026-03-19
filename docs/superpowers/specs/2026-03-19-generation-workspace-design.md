# Generation Workspace + Inference Pipeline Design

**Date:** 2026-03-19
**Status:** Draft

---

## 1. Overview

Build the complete generation pipeline: domain model for generation parameters and results, inference backend abstraction with a first implementation (StableDiffusion.NET), generation workspace UI with model/VAE/LoRA selection (A1111/Forge style), and deterministic parameter persistence for recreation.

### Success Criteria

1. User can select a checkpoint model from the local catalog
2. User can optionally select a VAE and one or more LoRAs with weights
3. User can enter positive/negative prompts
4. User can configure sampler, steps, CFG, seed, resolution
5. User can generate an image and see it in a gallery
6. Generation parameters are persisted for exact recreation
7. Generation runs as a background job with progress
8. The inference backend is pluggable — a second backend can be added without changing UI/application code

---

## 2. Domain Model

### New Enums

```csharp
public enum Sampler
{
    Euler, EulerA, DPMPlusPlus2M, DPMPlusPlus2MKarras, DPMPlusPlusSDE,
    DPMPlusPlusSDEKarras, DDIM, UniPC, LMS, Heun, DPM2, DPM2A
}

public enum Scheduler
{
    Normal, Karras, Exponential, SGMUniform
}
```

### GenerationParameters (Value Object)

Immutable snapshot of all generation settings — this is what gets persisted for deterministic recreation:

```csharp
public sealed record GenerationParameters
{
    public required string PositivePrompt { get; init; }
    public string NegativePrompt { get; init; } = string.Empty;
    public required Guid CheckpointModelId { get; init; }
    public Guid? VaeModelId { get; init; }
    public IReadOnlyList<LoraReference> Loras { get; init; } = [];
    public Sampler Sampler { get; init; } = Sampler.EulerA;
    public Scheduler Scheduler { get; init; } = Scheduler.Normal;
    public int Steps { get; init; } = 20;
    public double CfgScale { get; init; } = 7.0;
    public long Seed { get; init; } = -1; // -1 = random
    public int Width { get; init; } = 512;
    public int Height { get; init; } = 512;
    public int BatchSize { get; init; } = 1;
    public string? ClipSkip { get; init; }
}

public sealed record LoraReference(Guid ModelId, double Weight = 1.0);
```

### GenerationJob (Entity)

Extends the concept of a generation run, linked to a project:

```csharp
public class GenerationJob
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public GenerationParameters Parameters { get; private set; }
    public GenerationJobStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public IReadOnlyList<GeneratedImage> Images { get; private set; } = [];

    // Factory method, state transitions
}

public enum GenerationJobStatus
{
    Pending, Running, Completed, Failed, Cancelled
}
```

### GeneratedImage (Entity)

```csharp
public class GeneratedImage
{
    public Guid Id { get; private set; }
    public Guid GenerationJobId { get; private set; }
    public string FilePath { get; private set; }
    public long Seed { get; private set; } // Actual seed used
    public int Width { get; private set; }
    public int Height { get; private set; }
    public double GenerationTimeSeconds { get; private set; }
    public string ParametersJson { get; private set; } // Full snapshot for recreation
    public DateTimeOffset CreatedAt { get; private set; }
}
```

---

## 3. Inference Abstraction

### IInferenceBackend

```csharp
public interface IInferenceBackend
{
    string BackendId { get; }
    string DisplayName { get; }
    InferenceCapabilities Capabilities { get; }

    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default);
    Task<InferenceResult> GenerateAsync(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct = default);
    Task UnloadModelAsync(CancellationToken ct = default);
}

public record InferenceCapabilities(
    IReadOnlyList<ModelFamily> SupportedFamilies,
    IReadOnlyList<Sampler> SupportedSamplers,
    int MaxWidth, int MaxHeight,
    bool SupportsLoRA, bool SupportsVAE);

public record ModelLoadRequest(
    string CheckpointPath,
    string? VaePath,
    IReadOnlyList<LoraLoadInfo> Loras);

public record LoraLoadInfo(string Path, double Weight);

public record InferenceRequest(
    string PositivePrompt, string NegativePrompt,
    Sampler Sampler, Scheduler Scheduler,
    int Steps, double CfgScale, long Seed,
    int Width, int Height, int BatchSize);

public record InferenceProgress(int Step, int TotalSteps, string Phase);

public record InferenceResult(
    bool Success,
    IReadOnlyList<GeneratedImageData> Images,
    string? Error);

public record GeneratedImageData(byte[] ImageBytes, long Seed, double GenerationTimeSeconds);
```

### StableDiffusionCppBackend (First Implementation)

Uses the **StableDiffusion.NET** NuGet package (C# bindings for stable-diffusion.cpp):
- Loads GGUF/SafeTensors models
- Supports SD 1.5, SDXL
- CPU and GPU (CUDA) execution
- LoRA support
- Reports per-step progress

If StableDiffusion.NET is unavailable or has compatibility issues, fall back to a **MockInferenceBackend** that generates placeholder images (colored noise) for testing the pipeline end-to-end.

---

## 4. Application Layer

### GenerationService

```csharp
public class GenerationService
{
    // CreateGenerationJob — validates params, resolves model paths, enqueues job
    // GetJobStatus — returns job with images
    // ListJobsForProject — paginated job history
    // CloneParameters — copy params from existing job for re-use
    // GetAvailableBackends — list backends with capabilities
}
```

### GenerationJobHandler (IJobHandler)

Background job handler:
1. Load model (checkpoint + optional VAE + LoRAs)
2. Execute inference with progress reporting
3. Save generated images to project asset directory
4. Create GeneratedImage records with full parameter JSON snapshot
5. Update job status

---

## 5. Persistence

### New DbSets
- `GenerationJobs` — with JSON-stored `GenerationParameters`
- `GeneratedImages` — with file path and parameter snapshot

### EF Configuration
- `GenerationParameters` stored as JSON column (EF Core owned type or JSON conversion)
- `GeneratedImage.ParametersJson` is the full serialized snapshot
- Indexes on ProjectId, Status, CreatedAt

---

## 6. Presentation: Generation Workspace

### Replace ProjectDetail Placeholder

The project detail page becomes the generation workspace with these panels:

**Model Selection Panel:**
- Checkpoint dropdown (filtered from catalog by Type=Checkpoint, optionally by family)
- VAE dropdown (optional, filtered by Type=VAE)
- LoRA multi-select with weight slider per LoRA (filtered by Type=LoRA)
- Compatible family badge on each option

**Prompt Panel:**
- Positive prompt (multi-line MudTextField)
- Negative prompt (multi-line MudTextField)
- Prompt token count (future)

**Parameters Panel (collapsible):**
- Sampler dropdown
- Scheduler dropdown
- Steps slider (1-150, default 20)
- CFG Scale slider (1-30, default 7)
- Seed input (-1 for random)
- Width/Height dropdowns or inputs (multiples of 64)
- Batch size (1-8)

**Generate Button:**
- Primary action — enqueues generation job
- Shows progress while running (step X/Y)
- Disabled while a generation is in progress for this project

**Output Gallery:**
- Grid of generated images for this project
- Click to view full size with metadata overlay
- Shows seed, generation time, model name
- "Reuse Settings" button to clone parameters back to the workspace
- "Save Parameters" — parameters are already persisted with every generation

### New Standalone Page: `/generate`

Also create a quick-generate page not tied to a project for fast experimentation.

---

## 7. Deterministic Recreation

Every `GeneratedImage` stores the complete `GenerationParameters` as JSON in `ParametersJson`. This includes:
- Model IDs (resolved to file paths at generation time)
- All numeric parameters (steps, CFG, seed, dimensions)
- Sampler and scheduler
- LoRA references with weights
- Prompts

To recreate: load the parameters JSON, resolve current model paths, submit to the same backend with the same seed. Identical output (within floating-point precision of the backend).

---

## 8. Testing Strategy

### Domain Tests
- GenerationParameters validation (positive prompt required, steps > 0, valid dimensions)
- GenerationJob state transitions
- GeneratedImage creation

### Application Tests
- GenerationService — validates parameters, resolves model paths, enqueues job
- Parameter cloning

### Infrastructure Tests
- MockInferenceBackend — generates placeholder images
- GenerationJobHandler — full pipeline with mock backend
- EF persistence for GenerationJob and GeneratedImage

### E2E Tests
- Select model, enter prompt, generate (with mock backend)
- View generated image in gallery
- Reuse settings from previous generation

---

## 9. New Files

### Domain
- `Enums/Sampler.cs`, `Enums/Scheduler.cs`, `Enums/GenerationJobStatus.cs`
- `ValueObjects/GenerationParameters.cs`, `ValueObjects/LoraReference.cs`
- `Entities/GenerationJob.cs`, `Entities/GeneratedImage.cs`

### Application
- `Interfaces/IInferenceBackend.cs` + supporting records
- `Interfaces/IGenerationJobRepository.cs`
- `Services/GenerationService.cs`
- `DTOs/GenerationJobDto.cs`, `DTOs/GeneratedImageDto.cs`
- `Commands/CreateGenerationCommand.cs`

### Infrastructure
- `Inference/MockInferenceBackend.cs` (for testing pipeline)
- `Inference/StableDiffusionCppBackend.cs` (real backend — best effort)
- `Jobs/GenerationJobHandler.cs`
- `Persistence/Configurations/GenerationJobConfiguration.cs`
- `Persistence/Configurations/GeneratedImageConfiguration.cs`
- `Persistence/Repositories/GenerationJobRepository.cs`

### Web
- `Components/Pages/ProjectDetail.razor` (major rewrite — generation workspace)
- `Components/Pages/Generate.razor` (standalone quick-generate)
- `Components/Shared/ModelSelector.razor`
- `Components/Shared/LoraSelector.razor`
- `Components/Shared/ParameterPanel.razor`
- `Components/Shared/GenerationGallery.razor`
- `Components/Shared/ImageDetailDialog.razor`
