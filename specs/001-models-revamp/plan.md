# Implementation Plan: Models Page Revamp

**Branch**: `001-models-revamp` | **Date**: 2026-03-21 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-models-revamp/spec.md`

## Summary

Complete overhaul of the Models page from a tab-per-provider layout to a unified single-surface design with source chips (Local/CivitAI/HuggingFace), rich model cards with preview images, card/list view toggle, side detail drawer, progressive filter disclosure, per-provider download location settings, CivitAI provider fixes (ControlNet, sort, family, NSFW), and HuggingFace provider overhaul (file listing, type detection, previews).

## Technical Context

**Language/Version**: .NET 10 (C#)
**Primary Dependencies**: ASP.NET Core Blazor Server, MudBlazor 9.x, EF Core + SQLite
**Real-time**: SignalR (StudioHub) — used for download progress
**Testing**: xUnit + FluentAssertions + NSubstitute (unit/integration), Reqnroll + Playwright (E2E)
**Target Platform**: Windows (primary), Linux CI
**Project Type**: Modular monolith — Domain → Application → Infrastructure → Web

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|-----------|-------|--------|
| I. Modular Monolith | DownloadLocationSettings in Domain. ModelCardViewModel in Application. Provider changes in Infrastructure. UI in Web. No cross-layer violations. | ☑ |
| II. Correctness | Provider API errors handled with fallbacks. Download failures surface messages. Missing previews use placeholders. | ☑ |
| III. Test-Driven | Domain tests for DownloadLocationSettings. Infrastructure tests for provider mapping. E2E smoke test for Models page. | ☑ |
| IV. UX First | Download progress inline. Filter chips always visible. Side drawer preserves scroll. Help text on download settings. Empty states. | ☑ |
| V. Simplicity | Reuses MudDrawer, MudChipSet, MudTable. Follows existing provider patterns. No new DB tables. | ☑ |
| VI. Security | API tokens only sent to provider APIs. Preview images respect NSFW shield. Downloaded filenames from provider, not user input. | ☑ |

### Quality Gates (from Constitution)

- [ ] Code compiles with zero errors (`dotnet build -c Release`)
- [ ] All non-E2E tests pass (`dotnet test --filter "!~E2E"`)
- [ ] No regressions — existing test count MUST NOT decrease
- [ ] Cross-platform path handling verified
- [ ] Schema changes include CREATE TABLE IF NOT EXISTS in Program.cs — N/A (no new tables)
- [ ] New settings have always-visible help text — Download Locations
- [ ] Long-running operations report progress via SignalR — downloads
- [ ] New pages have nav menu entry and E2E smoke test — existing /models page, updated
- [ ] Conventional commit message format used

## Project Structure

### Documentation (this feature)

```text
specs/001-models-revamp/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0: technology decisions
├── data-model.md        # Phase 1: entity/VO definitions
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code Changes

```text
# Domain (1 new file)
src/StableDiffusionStudio.Domain/ValueObjects/DownloadLocationSettings.cs

# Application (1 new file)
src/StableDiffusionStudio.Application/DTOs/ModelCardViewModel.cs

# Infrastructure (2 modified files)
src/StableDiffusionStudio.Infrastructure/ModelSources/CivitAIProvider.cs     # FIX+ENHANCE
src/StableDiffusionStudio.Infrastructure/ModelSources/HuggingFaceProvider.cs  # OVERHAUL

# Web (5 new, 2 modified)
src/StableDiffusionStudio.Web/Components/Pages/Models.razor                  # REWRITE
src/StableDiffusionStudio.Web/Components/Shared/ModelCardUnified.razor       # NEW
src/StableDiffusionStudio.Web/Components/Shared/ModelListRow.razor           # NEW
src/StableDiffusionStudio.Web/Components/Shared/ModelDetailDrawer.razor      # NEW
src/StableDiffusionStudio.Web/Components/Shared/ModelFilterBar.razor         # NEW
src/StableDiffusionStudio.Web/Components/Pages/Settings.razor                # ADD section

# Tests (2 new, 1 modified)
tests/StableDiffusionStudio.Domain.Tests/ValueObjects/DownloadLocationSettingsTests.cs
tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/CivitAIProviderTests.cs  # ADD tests
tests/StableDiffusionStudio.E2E.Tests/Features/FullWorkflow.feature                    # ADD scenario
```

## Complexity Tracking

No constitution violations. All changes follow existing patterns.
