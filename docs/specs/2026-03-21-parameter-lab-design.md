# Parameter Lab — Design Specification

## Overview

The Parameter Lab is a dedicated experimentation workspace for systematically exploring how generation parameters affect image output. Users configure sweep axes (e.g., CFG Scale 1-10 step 0.5), run experiments as background jobs, view results in an interactive matrix grid, select winners to refine base parameters, and save optimized settings as presets.

## Goals

- Every field in GenerationParameters is sweepable — no hardcoded parameter list
- Support single-variable and two-variable (X/Y grid) experiments
- Fixed seed by default so the only variable is the swept parameter
- Server-side execution resilient to page navigation — Lab page is a live viewer, not a controller
- Iterative refinement loop: run sweep → pick winner → lock that value → sweep next parameter
- Experiments are persistent: save, clone, re-run with different models
- Works in both txt2img and img2img modes
- Cross-page integration: send images/parameters from Generate to Lab and vice versa

## Core Concepts

**Experiment** — A saved configuration defining base parameters, one or two sweep axes, and generation mode. Persists independently of projects.

**Sweep Axis** — A parameter name + list of values. Numeric parameters use start/end/step. Enum parameters (Sampler, Scheduler) use multi-select. Models use a checkpoint picker. Prompts use a text variation list.

**Run** — One execution of an experiment. Produces a matrix of images. Multiple runs per experiment (e.g., same sweep, different model). Runs execute as background jobs and survive page navigation.

**Winner** — A selected image whose parameters become the new base for the next experiment. This is the iterative refinement mechanism.

## Data Model

### ExperimentEntity

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| Name | string | User-given name, defaults to "Experiment — {date}" |
| Description | string? | Optional notes |
| BaseParametersJson | string | Full GenerationParameters serialized as JSON |
| InitImagePath | string? | Init image for img2img experiments |
| SweepAxesJson | string | List of SweepAxis serialized as JSON |
| CreatedAt | DateTimeOffset | |
| UpdatedAt | DateTimeOffset | |

### SweepAxis (value object, stored in JSON)

| Field | Type | Description |
|-------|------|-------------|
| ParameterName | string | Property name on GenerationParameters (e.g., "CfgScale") |
| Values | List\<string\> | Expanded values as strings (e.g., ["1.0", "1.5", "2.0"]) |
| Label | string? | Display label override (e.g., "CFG Scale") |

### ExperimentRunEntity

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| ExperimentId | Guid | FK to Experiment |
| FixedSeed | long | Seed used for all images (or -1 for random) |
| UseFixedSeed | bool | Whether fixed seed is active |
| TotalCombinations | int | Total cells in the grid |
| CompletedCount | int | Images generated so far |
| Status | ExperimentRunStatus | Pending, Running, Completed, Failed, Cancelled |
| ErrorMessage | string? | |
| StartedAt | DateTimeOffset? | |
| CompletedAt | DateTimeOffset? | |

### ExperimentRunImageEntity

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| RunId | Guid | FK to ExperimentRun |
| FilePath | string | Path to saved PNG |
| Seed | long | Actual seed used |
| GenerationTimeSeconds | double | |
| AxisValuesJson | string | e.g., {"CfgScale": "3.5", "Steps": "20"} |
| GridX | int | Column index in the matrix |
| GridY | int | Row index (0 for single-axis) |
| IsWinner | bool | Selected as winner by user |
| ContentRating | ContentRating | NSFW classification |
| NsfwScore | double | |

### ExperimentRunStatus (enum)

Pending, Running, Completed, Failed, Cancelled

## Sweep Engine

### Value Expansion

The SweepExpander service takes a SweepAxis definition and produces the list of concrete values:

- **Numeric (int):** start=5, end=25, step=5 → [5, 10, 15, 20, 25]
- **Numeric (double):** start=1.0, end=5.0, step=0.5 → [1.0, 1.5, 2.0, ..., 5.0]
- **Enum:** user picks from available values → [EulerA, DDIM, DPMPlusPlus2M]
- **Model:** user picks checkpoints → [guid1, guid2, guid3]
- **Prompt:** user enters text variations → ["a cat", "a dog", "a bird"]
- **Resolution:** user picks pairs → ["512x512", "768x1024", "1024x1024"]

### Cartesian Product

For two axes, generate all combinations:
- Axis1: [CfgScale 3.0, 5.0, 7.0] (3 values)
- Axis2: [Steps 10, 20, 30] (3 values)
- Result: 9 combinations, displayed as 3×3 grid

Single axis: N combinations displayed as 1×N row.

### Parameter Override

Each combination starts with a clone of BaseParameters and overrides the swept fields. The override is type-aware:

- Numeric fields → parse string to int/double via reflection or a type map
- Enum fields → Enum.Parse
- CheckpointModelId → parse as Guid
- PositivePrompt → direct string replacement
- Resolution → split "WxH" into Width and Height

### Model-Aware Batching

When the sweep includes CheckpointModelId, the runner groups combinations by model and processes all images for one model before moving to the next. This minimizes model swaps (30-120s each).

## Execution Model

### Background Job

Running an experiment creates a `JobRecord` with type "experiment" and data containing the ExperimentRun ID. The `ExperimentJobHandler` processes it:

1. Load the Experiment and Run from the database
2. Expand sweep axes into the full combination list
3. Group by model if sweeping models
4. For each combination:
   a. Clone base params, apply overrides
   b. Load model if needed (reuses if same as previous)
   c. Call `IInferenceBackend.GenerateAsync` with batch size 1
   d. Save image to disk under experiment assets directory
   e. Create ExperimentRunImage record with axis values and grid position
   f. Update Run.CompletedCount
   g. Send `ExperimentProgress` SignalR event
   h. Check cancellation token
5. On completion: update Run status, send `ExperimentComplete`
6. On failure: update Run status with error, send `ExperimentFailed`

### Resilience

- Run state is fully persisted in the database after each image
- If the app restarts, the run shows as partially complete
- The Lab page reconstructs the grid from database state on load
- If a run is still in progress, the page auto-connects to live SignalR updates
- Cancellation sets a flag that the handler checks between images

### SignalR Events

| Event | Parameters | Description |
|-------|-----------|-------------|
| ExperimentProgress | runId, completedIndex, totalCount, axisValuesJson, imageUrl | Fired after each image |
| ExperimentComplete | runId | All images done |
| ExperimentFailed | runId, error | Run failed |

## UI Layout

### Page Route: `/lab`

Three-panel layout within a MudGrid.

### Left Panel — Experiment Setup (~30%)

Top section:
- Experiment name text field
- Save / Clone / Delete buttons
- Saved experiments dropdown to load previous ones

Mode:
- txt2img / img2img toggle
- ImageUpload component when img2img selected

Model selection:
- Checkpoint selector (reuses ModelSelector)
- VAE selector (optional)
- LoRA selector

Prompt:
- Positive prompt (multiline)
- Negative prompt (multiline)

Base Parameters:
- Collapsible section containing the full ParameterPanel
- These are the defaults — anything not being swept uses these values
- Seed control with "Fixed seed" toggle (default on)

### Middle Panel — Sweep Configuration (~20%)

Axis 1 (columns):
- Dropdown to select parameter name (all GenerationParameters fields)
- Dynamic editor based on type:
  - Numeric: start, end, step fields
  - Enum: multi-select chips
  - Model: multi-select model picker
  - Prompt: text area with one variation per line
- Preview chips showing expanded values
- "Clear" button

Axis 2 (rows) — optional:
- Same as Axis 1
- Label: "Leave empty for single-variable sweep"

Summary:
- "15 images will be generated" count
- Estimated time (based on recent generation speeds)
- **Run Experiment** button (primary, prominent)
- Cancel button (visible during run)

### Right Panel — Results Grid (~50%)

Grid display:
- Column headers: Axis 1 values
- Row headers: Axis 2 values (or single row for 1D)
- Each cell: generated image thumbnail (square, object-fit:cover)
- Empty cells show skeleton placeholder during generation
- Currently generating cell shows progress spinner

Interaction:
- Click image → gold border, marked as winner
- Multiple winners allowed (for comparison)
- Hover → tooltip with full parameters

Action bar below grid:
- "Apply Winner" → copies winner's params to base parameters, clears sweep
- "Save as Preset" → opens save preset dialog with winner's params
- "Send to Generate" → navigates to /generate with params pre-filled
- "Download Grid" → stitches images into composite PNG with axis labels

Run history:
- Tabs or dropdown below the grid to switch between runs
- Shows timestamp and model name for each run

## Cross-Page Integration

### Generate → Lab

ImageDetailDialog and GenerationGallery get a new action: "Send to Lab"
- Navigates to `/lab?fromJob={jobId}`
- Lab page loads that job's GenerationParameters as base params
- For img2img, loads the generated image as init image

### Lab → Generate

"Send to Generate" button on selected winner:
- Navigates to `/generate?fromExperiment={runImageId}`
- Generate page loads the full parameters from that experiment image

### Lab → Presets

"Save as Preset" on selected winner:
- Opens the existing SavePresetDialog
- Pre-fills all parameters from the winning image's combination

## Grid Export

"Download Grid" composites all completed cells into a single PNG:
- Axis labels rendered as text headers (top row for X, left column for Y)
- Each cell scaled to a uniform size (e.g., 256px or 512px)
- Uses System.Drawing.Bitmap for composition (Windows-only, already a dependency)
- Downloads via JS interop (same pattern as settings export)

## File Structure

### Domain Layer
- `Entities/Experiment.cs`
- `Entities/ExperimentRun.cs`
- `Entities/ExperimentRunImage.cs`
- `Enums/ExperimentRunStatus.cs`
- `ValueObjects/SweepAxis.cs`
- `Services/SweepExpander.cs`

### Application Layer
- `Interfaces/IExperimentRepository.cs`
- `Interfaces/IExperimentService.cs`
- `Services/ExperimentService.cs`
- `DTOs/ExperimentDto.cs`
- `DTOs/ExperimentRunDto.cs`
- `DTOs/ExperimentRunImageDto.cs`
- `Commands/CreateExperimentCommand.cs`
- `Commands/RunExperimentCommand.cs`

### Infrastructure Layer
- `Persistence/Configurations/ExperimentConfiguration.cs`
- `Persistence/Configurations/ExperimentRunConfiguration.cs`
- `Persistence/Configurations/ExperimentRunImageConfiguration.cs`
- `Persistence/Repositories/ExperimentRepository.cs`
- `Jobs/ExperimentJobHandler.cs`
- `Services/GridCompositor.cs`

### Web Layer
- `Pages/Lab.razor`
- `Shared/SweepAxisEditor.razor`
- `Shared/ExperimentGrid.razor`
- `Shared/ExperimentList.razor`
- `Dialogs/GridExportDialog.razor`
- NavMenu entry: "Parameter Lab"

### SignalR
- Add `ExperimentProgress`, `ExperimentComplete`, `ExperimentFailed` to IGenerationNotifier (or create IExperimentNotifier)

### Database
- Migration adding Experiments, ExperimentRuns, ExperimentRunImages tables

## Sweepable Parameters (complete list)

Every field on GenerationParameters:

| Parameter | Type | Sweep UI |
|-----------|------|----------|
| PositivePrompt | string | Text variations (one per line) |
| NegativePrompt | string | Text variations (one per line) |
| CheckpointModelId | Guid | Multi-select model picker |
| VaeModelId | Guid? | Multi-select model picker |
| Sampler | enum | Multi-select chips |
| Scheduler | enum | Multi-select chips |
| Steps | int | Start / End / Step |
| CfgScale | double | Start / End / Step |
| Seed | long | Comma-separated list |
| Width | int | Resolution pair picker |
| Height | int | Resolution pair picker |
| BatchSize | int | Start / End / Step |
| ClipSkip | int | Start / End / Step |
| Eta | double | Start / End / Step |
| DenoisingStrength | double | Start / End / Step |
| HiresFixEnabled | bool | Toggle: include both on/off |
| HiresUpscaleFactor | double | Start / End / Step |
| HiresSteps | int | Start / End / Step |
| HiresDenoisingStrength | double | Start / End / Step |
| Loras | list | Multi-select with weight sweep |
