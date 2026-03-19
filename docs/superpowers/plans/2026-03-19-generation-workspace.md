# Generation Workspace + Inference Pipeline Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the complete generation pipeline — domain model, inference backend abstraction with mock + real backends, generation workspace UI with A1111-style model/VAE/LoRA selection, and deterministic parameter persistence.

**Architecture:** GenerationParameters value object captures all settings. IInferenceBackend is the pluggable execution contract. GenerationService orchestrates validation → job creation → backend dispatch → result persistence. Background jobs handle actual generation.

**Tech Stack:** .NET 10, EF Core JSON columns, StableDiffusion.NET (best effort), MudBlazor, existing job system

**Spec:** `docs/superpowers/specs/2026-03-19-generation-workspace-design.md`

---

## Task 1: Domain — Enums, Value Objects, Entities

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Enums/Sampler.cs`
- Create: `src/StableDiffusionStudio.Domain/Enums/Scheduler.cs`
- Create: `src/StableDiffusionStudio.Domain/Enums/GenerationJobStatus.cs`
- Create: `src/StableDiffusionStudio.Domain/ValueObjects/GenerationParameters.cs`
- Create: `src/StableDiffusionStudio.Domain/ValueObjects/LoraReference.cs`
- Create: `src/StableDiffusionStudio.Domain/Entities/GenerationJob.cs`
- Create: `src/StableDiffusionStudio.Domain/Entities/GeneratedImage.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/Entities/GenerationJobTests.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/ValueObjects/GenerationParametersTests.cs`

Implement all enums, GenerationParameters (immutable record with validation — positive prompt required, steps 1-150, dimensions multiples of 64, CFG 1-30), LoraReference, GenerationJob (factory + state machine: Create → Start → Complete/Fail/Cancel, AddImage), GeneratedImage. TDD with full tests.

Commit: `feat: add generation domain model with parameters, jobs, and images`

---

## Task 2: Inference Abstraction (IInferenceBackend)

**Files:**
- Create: `src/StableDiffusionStudio.Application/Interfaces/IInferenceBackend.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/InferenceCapabilities.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ModelLoadRequest.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/InferenceRequest.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/InferenceProgress.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/InferenceResult.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/GeneratedImageData.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/LoraLoadInfo.cs`

All interfaces and records from the spec. IInferenceBackend with IsAvailableAsync, LoadModelAsync, GenerateAsync, UnloadModelAsync.

Commit: `feat: define IInferenceBackend interface and supporting types`

---

## Task 3: GenerationService + Repository Interface

**Files:**
- Create: `src/StableDiffusionStudio.Application/Interfaces/IGenerationJobRepository.cs`
- Create: `src/StableDiffusionStudio.Application/Services/GenerationService.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/GenerationJobDto.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/GeneratedImageDto.cs`
- Create: `src/StableDiffusionStudio.Application/Commands/CreateGenerationCommand.cs`
- Create: `tests/StableDiffusionStudio.Application.Tests/Services/GenerationServiceTests.cs`

GenerationService: CreateAsync (validate params, resolve model paths from catalog, create GenerationJob, enqueue background job), GetJobAsync, ListJobsForProjectAsync, CloneParametersAsync. Tests with mocked repos and job queue.

Commit: `feat: add GenerationService with job creation and parameter validation`

---

## Task 4: Persistence — EF Configuration + Repository

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/GenerationJobConfiguration.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/GeneratedImageConfiguration.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/GenerationJobRepository.cs`
- Modify: `src/StableDiffusionStudio.Infrastructure/Persistence/AppDbContext.cs` — add GenerationJobs, GeneratedImages DbSets
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Persistence/GenerationJobRepositoryTests.cs`

GenerationParameters stored as JSON column. GeneratedImage.ParametersJson stores the full snapshot. Integration tests with in-memory SQLite.

Commit: `feat: add EF persistence for generation jobs and images`

---

## Task 5: MockInferenceBackend + GenerationJobHandler

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Inference/MockInferenceBackend.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/Jobs/GenerationJobHandler.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Inference/MockInferenceBackendTests.cs`

MockInferenceBackend: generates a solid color PNG (256x256 or requested dimensions) with seed-derived color. Returns GeneratedImageData with the bytes. Reports step progress. This enables testing the full pipeline without a real model.

GenerationJobHandler: loads model (via IInferenceBackend), runs generation, saves images to `{AppData}/Assets/{projectId}/{jobId}/`, creates GeneratedImage records, updates job status.

Commit: `feat: add MockInferenceBackend and GenerationJobHandler`

---

## Task 6: StableDiffusionCppBackend (Best Effort)

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Inference/StableDiffusionCppBackend.cs`

Try to use the StableDiffusion.NET NuGet package. If it's available and compatible with .NET 10:
- Implement LoadModelAsync (load GGUF/SafeTensors)
- Implement GenerateAsync (txt2img with sampler, steps, CFG, seed)
- Report per-step progress

If the package is not available or doesn't work on .NET 10, create a stub that returns `IsAvailableAsync = false` and falls back to MockInferenceBackend. The architecture is pluggable — we can swap in a real backend later.

Commit: `feat: add StableDiffusionCpp backend (or stub if unavailable)`

---

## Task 7: Generation Workspace UI Components

**Files:**
- Create: `src/StableDiffusionStudio.Web/Components/Shared/ModelSelector.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/LoraSelector.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/ParameterPanel.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Shared/GenerationGallery.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Dialogs/ImageDetailDialog.razor`

ModelSelector: MudSelect filtered by ModelType (Checkpoint/VAE), shows model title + family badge.

LoraSelector: MudChipSet or multi-select with MudSlider for weight per LoRA (0.0-2.0, default 1.0). Can add/remove LoRAs.

ParameterPanel: Collapsible panel with sampler/scheduler dropdowns, steps slider, CFG slider, seed input, width/height dropdowns (common resolutions: 512x512, 512x768, 768x512, 768x768, 1024x1024, 1024x1536, 1536x1024), batch size.

GenerationGallery: Grid of generated images with MudImage, shows seed/time overlay on hover. Click opens ImageDetailDialog.

ImageDetailDialog: Full-size image, metadata table (all parameters), "Reuse Settings" button.

Commit: `feat: add generation workspace UI components`

---

## Task 8: Wire Generation Workspace into ProjectDetail + Generate Page

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Components/Pages/ProjectDetail.razor` — replace placeholder with full workspace
- Create: `src/StableDiffusionStudio.Web/Components/Pages/Generate.razor` — standalone quick-generate at `/generate`
- Modify: `src/StableDiffusionStudio.Web/Components/Layout/NavMenu.razor` — add Generate nav link
- Modify: `src/StableDiffusionStudio.Web/Program.cs` — register GenerationService, IInferenceBackend, GenerationJobHandler, GenerationJobRepository

ProjectDetail.razor becomes the generation workspace:
- Top: model selection (checkpoint, VAE, LoRA)
- Middle: prompt inputs + parameter panel
- Bottom: Generate button + output gallery

Generate.razor: Same workspace but without project context — for quick experiments. Images saved to a default/scratch project.

DI registrations:
```csharp
builder.Services.AddScoped<GenerationService>();
builder.Services.AddScoped<IGenerationJobRepository, GenerationJobRepository>();
builder.Services.AddSingleton<IInferenceBackend, MockInferenceBackend>();
builder.Services.AddKeyedScoped<IJobHandler, GenerationJobHandler>("generation");
```

Commit: `feat: wire generation workspace into ProjectDetail and Generate page`

---

## Task 9: Final Integration and E2E Verification

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Components/Pages/Home.razor` — add generation shortcut
- Verify all tests pass
- Push to main

Verify the full flow:
1. Launch app, go to a project
2. Select a checkpoint model
3. Enter a prompt
4. Click Generate
5. See progress in real-time
6. See generated image (mock — solid color) in gallery
7. Click image → see full parameters
8. Click "Reuse Settings" → parameters populate workspace
9. Check Jobs page shows the generation job

Commit: `feat: complete generation workspace with mock inference backend`

---

## Summary

| Task | Description | Key Outputs |
|------|-------------|-------------|
| 1 | Domain model | Sampler/Scheduler enums, GenerationParameters, GenerationJob, GeneratedImage |
| 2 | Inference abstraction | IInferenceBackend, all supporting DTOs |
| 3 | GenerationService | Job creation, validation, parameter cloning |
| 4 | EF persistence | GenerationJob/Image configs, JSON columns, repository |
| 5 | Mock backend + handler | MockInferenceBackend, GenerationJobHandler |
| 6 | Real backend (best effort) | StableDiffusionCppBackend or stub |
| 7 | UI components | ModelSelector, LoraSelector, ParameterPanel, Gallery |
| 8 | Workspace wiring | ProjectDetail rewrite, Generate page, DI |
| 9 | Integration | E2E verification, push |
