# Milestone 1+ Design: Foundational Shell

**Date:** 2026-03-18
**Status:** Draft
**Approach:** Vertical Slice, TDD-first

---

## 1. Overview

Milestone 1+ delivers the foundational shell of Stable Diffusion Studio: a working Blazor Server app with Aspire orchestration, project management, local model folder scanning with metadata parsing, background job infrastructure, settings, and a polished MudBlazor UI. The "+" scope adds basic local folder model scanning so real models appear in the UI from day one.

### Success Criteria

A user can:
1. Launch the app via Aspire and see the dashboard
2. Create, rename, archive, pin, and delete projects
3. Configure a local model storage directory
4. Trigger a scan that discovers `.safetensors`/`.ckpt` files, parses metadata (family, format, size, preview)
5. Browse discovered models in a catalog UI
6. See scan progress via background job status
7. View telemetry in the Aspire dashboard

---

## 2. Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Blazor hosting | Interactive Server (SignalR) | Local-first, near-zero latency, full .NET API access |
| Scope | Full M1 + local folder scanning with metadata | Real models in UI from day one |
| Solution structure | 6 core projects | Lean — extract when a second consumer appears |
| Aspire | Full dashboard + resources | Invest in developer experience early |
| Persistence | EF Core code-first with migrations, SQLite | Standard .NET approach, clean separation |
| Background jobs | Hosted Services + System.Threading.Channels | Lightweight, no external dependencies |
| Testing | Full TDD with bUnit | Maximum confidence, domain through UI |
| Architecture approach | Vertical Slice | Working software at every step |

---

## 3. Solution Structure

```
/StableDiffusionStudio.sln
/src
  /StableDiffusionStudio.AppHost           → Aspire orchestration, dashboard, resource wiring
  /StableDiffusionStudio.ServiceDefaults   → OpenTelemetry, health checks, shared config
  /StableDiffusionStudio.Web               → Blazor Server, MudBlazor UI, SignalR hubs
  /StableDiffusionStudio.Application       → Use cases, services, DTOs, validation, interfaces
  /StableDiffusionStudio.Domain            → Entities, value objects, domain services, enums
  /StableDiffusionStudio.Infrastructure    → EF Core, SQLite, file system, model scanning, jobs
/tests
  /StableDiffusionStudio.Domain.Tests
  /StableDiffusionStudio.Application.Tests
  /StableDiffusionStudio.Infrastructure.Tests
  /StableDiffusionStudio.Web.Tests         → bUnit component tests
```

### Dependency Flow

- `Web` → `Application` → `Domain`
- `Infrastructure` → `Application`, `Domain`
- `AppHost` → `Web` (orchestration only)
- `ServiceDefaults` → referenced by `Web` and `AppHost`

Abstractions (`IModelSourceAdapter`, etc.) live in `Application`. No separate Abstractions project until a second consumer emerges.

---

## 4. Domain Model

### Entities

**Project** — Aggregate root.
- `Id` (Guid), `Name`, `Description`, `CreatedAt`, `UpdatedAt`, `Status`, `IsPinned`
- Behavior: `Rename(name)`, `Archive()`, `Restore()`, `Pin()`, `Unpin()`, `UpdateDescription(desc)`
- Invariants: Name is required and non-empty. Cannot rename an archived project.

**ModelRecord** — Represents a known model in the local catalog.
- `Id` (Guid), `Title`, `ModelFamily` (enum), `Format` (enum), `FilePath` (string), `FileSize` (long), `Checksum` (string?), `Source` (string — adapter name), `Tags` (IReadOnlyList\<string\>), `Description` (string?), `PreviewImagePath` (string?), `CompatibilityHints` (string?), `DetectedAt` (DateTimeOffset), `LastVerifiedAt` (DateTimeOffset?), `Status` (enum)
- Behavior: `MarkMissing()`, `MarkAvailable()`, `UpdateMetadata(...)`
- Invariants: FilePath is required. Title defaults to filename if not provided.

### Value Objects

- **ModelIdentifier** — `Source` + `ExternalId`. Equality-based. Uniquely identifies a model across adapters.
- **FileLocation** — Wraps a validated path string. Immutable.
- **StorageRoot** — `Path` + `DisplayName`. Immutable. Validates path is non-empty.
- **Resolution** — Deferred to Milestone 3 (generation workspace).
- **PromptText** — Deferred to Milestone 3 (generation workspace).

### Enums

- `ModelFamily` — SD15, SDXL, Flux, Unknown
- `ModelFormat` — SafeTensors, CKPT, GGUF, Diffusers, Unknown
- `ProjectStatus` — Active, Archived, Deleted
- `ModelStatus` — Available, Missing, Scanning
- `JobStatus` — Pending, Running, Completed, Failed, Cancelled

### Domain Services

**ModelFileAnalyzer** — Pure logic for inferring `ModelFamily` and `Format` from file extension, size heuristics, and embedded metadata headers. No I/O — receives byte arrays and file info as input.

### Deferred to Later Milestones

`GenerationRequest`, `GenerationJob`, `Asset`, `Preset`, `DownloadJob` — defined in the spec but not implemented until inference backends arrive (Milestone 3).

---

## 5. Application Layer

### Interfaces (defined in Application, implemented in Infrastructure)

```csharp
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAsync(ProjectFilter filter, CancellationToken ct);
    Task AddAsync(Project project, CancellationToken ct);
    Task UpdateAsync(Project project, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public interface IModelCatalogRepository
{
    Task<ModelRecord?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ModelRecord>> ListAsync(ModelFilter filter, CancellationToken ct);
    Task<ModelRecord?> GetByFilePathAsync(string filePath, CancellationToken ct);
    Task UpsertAsync(ModelRecord record, CancellationToken ct);
    Task RemoveAsync(Guid id, CancellationToken ct);
}

public interface IModelSourceAdapter
{
    string SourceName { get; }
    Task<IReadOnlyList<ModelRecord>> ScanAsync(StorageRoot root, CancellationToken ct);
    ModelSourceCapabilities GetCapabilities();
}

public interface IStorageRootProvider
{
    Task<IReadOnlyList<StorageRoot>> GetRootsAsync(CancellationToken ct);
    Task AddRootAsync(StorageRoot root, CancellationToken ct);
    Task RemoveRootAsync(string path, CancellationToken ct);
}

public interface IJobQueue
{
    Task<Guid> EnqueueAsync(JobDefinition job, CancellationToken ct);
    Task<JobRecord?> GetStatusAsync(Guid jobId, CancellationToken ct);
    Task<IReadOnlyList<JobRecord>> ListActiveAsync(CancellationToken ct);
    Task CancelAsync(Guid jobId, CancellationToken ct);
}

public interface ISettingsProvider
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct);
    Task SetAsync<T>(string key, T value, CancellationToken ct);
}
```

### Application Services

**ProjectService** — Create, rename, archive, delete, pin, list, search. Thin orchestration — delegates invariants to the `Project` entity. Publishes `ProjectChangedEvent` after mutations.

**ModelCatalogService** — Triggers scans by enqueuing a scan job, queries the catalog, registers newly discovered models, marks missing models. Coordinates between `IModelSourceAdapter` and `IModelCatalogRepository`.

**SettingsService** — Read/write settings with typed access. Validates at boundaries.

**JobOrchestrationService** — Submit jobs, query status, cancel. Wraps `IJobQueue` with correlation IDs and structured logging.

### Commands and DTOs

- `CreateProjectCommand` { Name, Description? } → `ProjectDto`
- `RenameProjectCommand` { Id, NewName }
- `ScanModelsCommand` { StorageRootPath? } (null = scan all roots)
- `AddStorageRootCommand` { Path, DisplayName }
- `UpdateSettingCommand` { Key, Value }
- `ProjectDto`, `ModelRecordDto`, `JobRecordDto`, `SettingsDto`

### Validation

FluentValidation validators for each command. Validated at the service boundary.

### In-Process Events

- `ProjectChangedEvent` { ProjectId, ChangeType }
- `ModelScanCompletedEvent` { StorageRoot, NewCount, UpdatedCount, MissingCount }
- `JobProgressEvent` { JobId, Progress, Phase }

Implemented via simple callback delegates or `IObservable<T>`. No MediatR dependency — keep it lightweight. Can upgrade later if needed.

---

## 6. Infrastructure Layer

### Persistence

**`AppDbContext`** — Single EF Core DbContext with DbSets for `Project`, `ModelRecord`, `JobRecord`, `Setting`.

Located in `Infrastructure/Persistence/`. Configuration via Fluent API in separate `IEntityTypeConfiguration<T>` classes.

**Migrations:** EF Core code-first migrations stored in `Infrastructure/Persistence/Migrations/`.

**Connection:** SQLite with a configurable path, defaulting to `AppData/Database/studio.db`. Registered as scoped via Aspire resource wiring.

### Local Folder Model Scanning

**`LocalFolderAdapter : IModelSourceAdapter`**
- Scans configured directories recursively for `.safetensors`, `.ckpt`, `.gguf` files
- For each file: reads file size, extension, and first N bytes of header
- Delegates to `ModelFileAnalyzer` (domain) for family/format inference
- Looks for sidecar files: `.preview.png`, `.json` metadata, `.yaml` config
- Returns normalized `ModelRecord` instances

**File format detection heuristics:**
- `.safetensors` → parse header JSON for tensor names, infer family from architecture keys
- `.ckpt` → size-based heuristics (SD 1.5 ~2-4GB, SDXL ~6-7GB)
- `.gguf` → parse GGUF header for metadata
- Sidecar `.json` → look for `modelspec.*` or civitai-style metadata fields

### Background Jobs

**`BackgroundJobProcessor : BackgroundService`**
- Reads from a `Channel<JobDefinition>`
- Executes jobs sequentially (single worker initially, configurable later)
- Updates `JobRecord` in SQLite with status, progress, timestamps, errors
- Publishes `JobProgressEvent` for real-time UI updates

**`JobRecord`** entity (in Domain):
- `Id`, `Type`, `Status`, `Progress` (0-100), `Phase`, `CorrelationId`, `CreatedAt`, `StartedAt`, `CompletedAt`, `ErrorMessage`, `ResultData`

### SignalR

**`StudioHub`** — Single hub for real-time UI updates.
- Methods: `JobProgress(jobId, progress, phase)`, `ModelScanUpdate(...)`, `ProjectChanged(...)`
- Infrastructure services push updates through `IHubContext<StudioHub>`

### Settings Storage

`SettingsProvider` backed by a `Setting` table in SQLite (Key/Value with JSON-serialized values). Cached in-memory with invalidation on write.

---

## 7. Presentation Layer (Web)

### App Shell

MudBlazor `MudLayout` with:
- **Left drawer** — Navigation rail: Home, Projects, Models, Jobs, Settings
- **Top app bar** — App title, search placeholder (wired later), theme toggle, notification icon
- **Main content area** — Routed pages

Dark theme by default. Custom MudTheme with neutral palette and blue accent (per spec: "modern dark-first creative-tool feel").

### Pages

**Home Dashboard** (`/`)
- Quick-create project button (prominent)
- Recent projects list (pinned first)
- Model catalog summary (count of discovered models)
- Active jobs status
- Storage health (total models, disk usage)

**Projects** (`/projects`)
- List/grid of projects with status badges
- Search/filter
- Create, rename, archive, delete actions

**Project Detail** (`/projects/{id}`)
- Project name, description (editable inline)
- Model selector (from catalog) — placeholder for generation workspace in M3
- Activity timeline placeholder

**Models** (`/models`)
- Card grid of discovered models
- Filter by family, format, source
- Search by name/tags
- Model detail panel (metadata, file info, preview image)
- "Add Storage Root" action
- "Scan Now" button with progress indicator

**Jobs** (`/jobs`)
- Active and completed jobs list
- Progress bars, timestamps, status
- Error details expandable

**Settings** (`/settings`)
- Storage roots management (add/remove directories)
- Appearance (theme toggle)
- Diagnostics info

### Component Design

Small, composable components:
- `ProjectCard`, `ModelCard`, `JobProgressCard`
- `StorageRootEditor`, `QuickCreateProject`
- `EmptyState` — friendly message + CTA when no data exists

### State & Real-Time

- Services injected as scoped
- SignalR client connection in `App.razor` or a `CircuitHandler`
- Hub events trigger `StateHasChanged()` on relevant components via injected notification service

---

## 8. Aspire Integration

**AppHost** configures:
- SQLite as a connection string resource
- Web project as the main service
- Aspire dashboard enabled with OpenTelemetry
- Health checks for the web app

**ServiceDefaults** provides:
- `AddServiceDefaults()` extension wiring OpenTelemetry (traces, metrics, logs), health checks, and HTTP resilience
- Used by the Web project

**Observability from day one:**
- Structured logging via `ILogger` → visible in Aspire dashboard
- Activity traces for model scanning, job execution
- Custom metrics: `models.scanned`, `jobs.completed`, `projects.created`

---

## 9. Testing Strategy

### Domain Tests (`Domain.Tests`)
- Entity behavior: `Project` state transitions, invariant enforcement
- Value object equality and validation
- `ModelFileAnalyzer` — feed it known headers, assert correct family/format detection
- **xUnit + FluentAssertions**

### Application Tests (`Application.Tests`)
- Service orchestration with mocked repositories
- Command validation (FluentValidation)
- Event publishing behavior
- **xUnit + NSubstitute + FluentAssertions**

### Infrastructure Tests (`Infrastructure.Tests`)
- EF Core with in-memory SQLite for repository tests
- `LocalFolderAdapter` with a test fixture directory containing sample model files
- Background job processor integration
- **xUnit + actual SQLite**

### Web Tests (`Web.Tests`)
- bUnit component tests for key components
- Page rendering with mocked services
- Navigation behavior
- **bUnit + xUnit + NSubstitute**

### Test Naming Convention
`MethodName_Scenario_ExpectedResult` — e.g., `Rename_WhenArchived_ThrowsInvalidOperationException`

---

## 10. Vertical Slice Build Order

The implementation follows this sequence, with TDD at each step:

1. **Solution scaffold** — Create solution, 6 src projects, 4 test projects, wire Aspire, verify `dotnet build` and Aspire launch
2. **Domain core** — `Project` entity, value objects, enums, domain tests
3. **Persistence** — `AppDbContext`, `Project` configuration, migrations, repository, integration tests
4. **Project service** — `ProjectService`, validation, application tests
5. **App shell UI** — MudBlazor layout, navigation, dark theme, home dashboard, bUnit smoke tests
6. **Project CRUD UI** — Projects page, create/rename/archive/delete, wired to real persistence
7. **Domain: ModelRecord** — Entity, `ModelFileAnalyzer`, value objects, domain tests
8. **Model scanning infrastructure** — `LocalFolderAdapter`, catalog repository, integration tests with fixture files
9. **Model catalog service** — Application orchestration, validation, tests
10. **Models UI** — Model browser page, cards, filters, storage root config
11. **Background jobs** — `JobRecord`, `Channel`-based queue, `BackgroundJobProcessor`, wire model scanning as a job
12. **Jobs UI** — Job center page, progress indicators, SignalR real-time updates
13. **Settings** — Domain/infrastructure/service/UI for storage roots and appearance
14. **Polish** — Empty states, error handling, Aspire telemetry verification, final integration pass

---

## 11. Key Risks and Mitigations

| Risk | Mitigation |
|---|---|
| SafeTensors header parsing complexity | Start with extension + size heuristics, refine with header parsing incrementally |
| bUnit + MudBlazor compatibility | Verify in slice 5 before committing to heavy component testing |
| Aspire 13+ breaking changes | Pin Aspire version, use stable APIs only |
| Scope creep from model scanning | Strict boundary — scan and catalog only, no download/import in M1+ |
| SQLite concurrency under background jobs | Single writer via Channel, EF scoped contexts per operation |
