# Architecture Overview

Stable Diffusion Studio is built as a **modular monolith** on .NET 10 with clear layer boundaries and a vertical-slice development approach.

## System Layers

```
┌─────────────────────────────────────────────┐
│              Presentation                    │
│   Blazor Server + MudBlazor + SignalR       │
├─────────────────────────────────────────────┤
│              Application                     │
│   Services, Commands, DTOs, Validation      │
│   Interfaces (IProjectRepository, etc.)     │
├─────────────────────────────────────────────┤
│              Domain                          │
│   Entities, Value Objects, Enums            │
│   Domain Services (ModelFileAnalyzer)       │
├─────────────────────────────────────────────┤
│              Infrastructure                  │
│   EF Core + SQLite, File System             │
│   Model Source Adapters, Background Jobs    │
│   Settings, Telemetry                       │
└─────────────────────────────────────────────┘
```

## Dependency Direction

Dependencies flow **inward** — outer layers depend on inner layers, never the reverse.

- **Presentation** → Application → Domain
- **Infrastructure** → Application, Domain (via interfaces)
- **Domain** has zero external dependencies

## Solution Structure

| Project | Layer | Purpose |
|---------|-------|---------|
| `StableDiffusionStudio.Domain` | Domain | Entities, value objects, enums, domain services |
| `StableDiffusionStudio.Application` | Application | Use-case orchestration, DTOs, validation, interfaces |
| `StableDiffusionStudio.Infrastructure` | Infrastructure | EF Core, file I/O, model scanning, background jobs |
| `StableDiffusionStudio.Web` | Presentation | Blazor Server UI, MudBlazor components, SignalR hubs |
| `StableDiffusionStudio.AppHost` | Orchestration | .NET Aspire dev orchestration and dashboard |
| `StableDiffusionStudio.ServiceDefaults` | Cross-cutting | OpenTelemetry, health checks, resilience |

## Key Patterns

### Rich Domain Model
Entities protect their own state with private setters and intention-revealing methods. Factory methods enforce invariants at creation time.

### Repository Pattern
Repository interfaces defined in Application, implemented in Infrastructure. EF Core is an implementation detail hidden behind `IProjectRepository`, `IModelCatalogRepository`.

### Adapter Pattern
Model sources implement `IModelSourceAdapter` to normalize source-specific metadata into canonical `ModelRecord` entities.

### Background Job Processing
`Channel<T>`-based job queue with `BackgroundService` processor. Jobs are persisted to SQLite for durability. Keyed `IJobHandler` implementations handle specific job types.

## Data Flow

### Model Scanning
1. User configures storage roots (Settings)
2. User triggers scan (Models page)
3. `ModelCatalogService` iterates storage roots × adapters
4. `LocalFolderAdapter` scans directories, uses `ModelFileAnalyzer` for metadata
5. Discovered models are upserted into the catalog via `IModelCatalogRepository`
6. UI refreshes to show discovered models

### Project Lifecycle
1. User creates project (Dashboard or Projects page)
2. `ProjectService` creates `Project` entity via factory method
3. `IProjectRepository` persists to SQLite
4. Project appears in list, navigable to detail page
5. User can rename, pin, archive, delete

## Technology Stack

- .NET 10, ASP.NET Core
- Blazor Web App (Interactive Server)
- MudBlazor (UI components)
- .NET Aspire 13+ (orchestration, dashboard)
- EF Core + SQLite (persistence)
- SignalR (real-time updates)
- FluentValidation (command validation)
- OpenTelemetry (diagnostics)

## Testing Strategy

| Layer | Framework | Approach |
|-------|-----------|----------|
| Domain | xUnit + FluentAssertions | Unit tests for entities, value objects, domain services |
| Application | xUnit + NSubstitute + FluentAssertions | Unit tests with mocked repositories |
| Infrastructure | xUnit + SQLite in-memory | Integration tests with real database |
| E2E | Playwright + Reqnroll | BDD feature files with browser automation |

## ADRs

See [docs/adrs/](../adrs/) for architectural decision records:
- [ADR-001](../adrs/ADR-001-blazor-web-app-with-mudblazor.md): Blazor + MudBlazor
- [ADR-002](../adrs/ADR-002-modular-monolith.md): Modular monolith
- [ADR-003](../adrs/ADR-003-sqlite-default-persistence.md): SQLite persistence
- [ADR-004](../adrs/ADR-004-pluggable-inference-backends.md): Pluggable inference
- [ADR-005](../adrs/ADR-005-adapter-driven-model-sources.md): Adapter-driven model sources
- [ADR-006](../adrs/ADR-006-local-first-privacy.md): Local-first privacy
