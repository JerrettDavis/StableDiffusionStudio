# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

**Language/Version**: .NET 10 (C#)
**Primary Dependencies**: ASP.NET Core Blazor Server, MudBlazor 9.x, EF Core + SQLite
**Real-time**: SignalR (StudioHub)
**Inference**: StableDiffusion.NET (stable-diffusion.cpp) via CUDA/Vulkan/CPU
**Testing**: xUnit + FluentAssertions + NSubstitute (unit/integration), Reqnroll + Playwright (E2E)
**Target Platform**: Windows (primary), Linux CI
**Project Type**: Modular monolith — Domain → Application → Infrastructure → Web

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|-----------|-------|--------|
| I. Modular Monolith | Dependencies flow inward? No cross-layer violations? | ☐ |
| II. Correctness | Builds clean? All tests pass? No swallowed exceptions? | ☐ |
| III. Test-Driven | Layer-appropriate tests included? Cross-platform safe? | ☐ |
| IV. UX First | Real-time progress via SignalR? Help text on settings? Empty states? | ☐ |
| V. Simplicity | Follows existing patterns? No premature abstractions? YAGNI? | ☐ |
| VI. Security | No secret logging? External data validated? Filenames sanitized? | ☐ |

### Quality Gates (from Constitution)

- [ ] Code compiles with zero errors (`dotnet build -c Release`)
- [ ] All non-E2E tests pass (`dotnet test --filter "!~E2E"`)
- [ ] No regressions — existing test count MUST NOT decrease
- [ ] Cross-platform path handling verified
- [ ] Schema changes include CREATE TABLE IF NOT EXISTS in Program.cs
- [ ] New settings have always-visible help text
- [ ] Long-running operations report progress via SignalR
- [ ] New pages have nav menu entry and E2E smoke test
- [ ] Conventional commit message format used

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (Stable Diffusion Studio)

```text
src/
├── StableDiffusionStudio.Domain/         # Entities, ValueObjects, Enums, Services
│   ├── Entities/                         # Aggregate roots, child entities
│   ├── ValueObjects/                     # Immutable records
│   ├── Enums/                            # Domain enumerations
│   └── Services/                         # Domain services (static/pure)
├── StableDiffusionStudio.Application/    # Use-case orchestration
│   ├── Interfaces/                       # Repository + service contracts
│   ├── DTOs/                             # Data transfer objects (records)
│   ├── Services/                         # Application services
│   └── Commands/                         # Command records
├── StableDiffusionStudio.Infrastructure/ # External concerns
│   ├── Persistence/                      # EF Core (AppDbContext, Configs, Repos)
│   ├── Inference/                        # StableDiffusion.NET backend
│   ├── Jobs/                             # Background job handlers (IJobHandler)
│   ├── Services/                         # Infrastructure services
│   ├── ModelSources/                     # CivitAI, HuggingFace, LocalFolder
│   └── Settings/                         # Settings providers
└── StableDiffusionStudio.Web/            # Blazor Server presentation
    ├── Components/Pages/                 # Routable pages
    ├── Components/Shared/                # Reusable components
    ├── Components/Dialogs/               # MudDialog components
    ├── Components/Layout/                # MainLayout, NavMenu
    ├── Hubs/                             # SignalR (StudioHub, Notifiers)
    └── wwwroot/                          # Static assets, JS interop

tests/
├── StableDiffusionStudio.Domain.Tests/         # Unit tests
├── StableDiffusionStudio.Application.Tests/    # Unit tests (mocked deps)
├── StableDiffusionStudio.Infrastructure.Tests/ # Integration tests
└── StableDiffusionStudio.E2E.Tests/            # Reqnroll + Playwright BDD
```

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., new layer] | [current need] | [why existing layers insufficient] |
