---

description: "Task list template for Stable Diffusion Studio feature implementation"
---

# Tasks: [FEATURE NAME]

**Input**: Design documents from `/specs/[###-feature-name]/`
**Prerequisites**: plan.md (required), spec.md (required for user stories)

**Constitution**: All tasks MUST comply with `.specify/memory/constitution.md`.
Key enforcement points are called out with ⚖️ markers below.

**Organization**: Tasks are grouped by architectural layer (Domain →
Application → Infrastructure → Web) following the modular monolith
dependency direction, then by user story for cross-cutting features.

## Format: `[ID] [P?] [Layer] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Layer]**: DOM (Domain), APP (Application), INF (Infrastructure), WEB (Web)
- Include exact file paths in descriptions

## Path Conventions (Stable Diffusion Studio)

```text
src/StableDiffusionStudio.Domain/          # Entities/, ValueObjects/, Enums/, Services/
src/StableDiffusionStudio.Application/     # Interfaces/, DTOs/, Services/, Commands/
src/StableDiffusionStudio.Infrastructure/  # Persistence/, Jobs/, Services/, ModelSources/
src/StableDiffusionStudio.Web/             # Components/Pages/, Components/Shared/, Hubs/
tests/StableDiffusionStudio.Domain.Tests/
tests/StableDiffusionStudio.Application.Tests/
tests/StableDiffusionStudio.Infrastructure.Tests/
tests/StableDiffusionStudio.E2E.Tests/
```

---

## Phase 1: Domain Layer

**Purpose**: Entities, value objects, enums, domain services

⚖️ Constitution III (Test-Driven): Each entity/VO MUST have unit tests.
⚖️ Constitution I (Architecture): No external dependencies in Domain.

- [ ] T001 [P] [DOM] Create enum in src/.../Enums/
- [ ] T002 [P] [DOM] Create value object in src/.../ValueObjects/
- [ ] T003 [DOM] Create domain service in src/.../Services/
- [ ] T004 [P] [DOM] Create entity in src/.../Entities/
- [ ] T005 [P] [DOM] Write unit tests in tests/.../Domain.Tests/

**Checkpoint**: Domain compiles, domain tests pass.

```bash
dotnet test tests/StableDiffusionStudio.Domain.Tests --no-restore -v quiet
```

---

## Phase 2: Application Layer

**Purpose**: Interfaces, DTOs, service contracts

⚖️ Constitution I (Architecture): Application depends on Domain only.
⚖️ Constitution V (Simplicity): DTOs are positional records, no logic.

- [ ] T006 [P] [APP] Create DTOs in src/.../DTOs/
- [ ] T007 [P] [APP] Create repository interface in src/.../Interfaces/
- [ ] T008 [P] [APP] Create service interface in src/.../Interfaces/
- [ ] T009 [P] [APP] Create notifier interface in src/.../Interfaces/ (if SignalR needed)

**Checkpoint**: Application compiles.

```bash
dotnet build src/StableDiffusionStudio.Application --no-restore
```

---

## Phase 3: Infrastructure Layer

**Purpose**: EF Core persistence, job handlers, external adapters

⚖️ Constitution I (Architecture): Infrastructure implements Application interfaces.
⚖️ Constitution Quality Gates: Schema changes need CREATE TABLE IF NOT EXISTS.

- [ ] T010 [P] [INF] Create EF Core configurations in src/.../Persistence/Configurations/
- [ ] T011 [INF] Add DbSets to AppDbContext
- [ ] T012 [INF] Add CREATE TABLE IF NOT EXISTS to Program.cs schema repair
- [ ] T013 [INF] Create repository implementation in src/.../Persistence/Repositories/
- [ ] T014 [INF] Create application service in src/.../Services/ (or Application layer)
- [ ] T015 [INF] Create job handler in src/.../Jobs/ (if background processing needed)

**Checkpoint**: Infrastructure compiles, schema repair includes new tables.

```bash
dotnet build src/StableDiffusionStudio.Infrastructure --no-restore
```

---

## Phase 4: Web Layer — DI & SignalR

**Purpose**: Wire everything together

⚖️ Constitution IV (UX): Long-running ops MUST use SignalR for progress.

- [ ] T016 [WEB] Register services in Program.cs DI container
- [ ] T017 [P] [WEB] Create SignalR notifier in src/.../Hubs/
- [ ] T018 [WEB] Update StudioHub.cs comments for new events

**Checkpoint**: Full solution builds clean.

```bash
dotnet build --no-restore -c Release
```

---

## Phase 5: Web Layer — UI Components

**Purpose**: Pages, shared components, dialogs

⚖️ Constitution IV (UX): Help text on settings, empty states, progress feedback.
⚖️ Constitution V (Simplicity): Reuse existing components (ModelSelector,
ParameterPanel, ImageUpload, etc.)

- [ ] T019 [P] [WEB] Create shared component in src/.../Components/Shared/
- [ ] T020 [P] [WEB] Create shared component in src/.../Components/Shared/
- [ ] T021 [WEB] Create page in src/.../Components/Pages/
- [ ] T022 [WEB] Add nav menu entry in NavMenu.razor

**Checkpoint**: UI renders, page accessible via nav.

---

## Phase 6: Cross-Page Integration

**Purpose**: Wire into existing pages (Generate, Lab, Models, etc.)

- [ ] T023 [WEB] Add actions to ImageDetailDialog (if applicable)
- [ ] T024 [WEB] Handle query parameters in new page (if applicable)
- [ ] T025 [WEB] Add to Settings page (if new settings needed)

---

## Phase 7: Polish & Verification

**Purpose**: Final quality pass

⚖️ Constitution Quality Gates: ALL checks MUST pass.

- [ ] T026 [WEB] Add E2E scenario in tests/.../E2E.Tests/Features/
- [ ] T027 Run full test suite and verify no regressions

```bash
dotnet build --no-restore -c Release
dotnet test --filter "FullyQualifiedName!~E2E" --no-restore -v quiet
```

- [ ] T028 Commit and push to main

---

## Dependencies & Execution Order

### Layer Dependencies (Constitution Principle I)

```
Phase 1 (Domain)     → no dependencies
Phase 2 (Application) → depends on Phase 1
Phase 3 (Infrastructure) → depends on Phase 2
Phase 4 (DI/SignalR)  → depends on Phase 3
Phase 5 (UI)          → depends on Phase 4
Phase 6 (Integration) → depends on Phase 5
Phase 7 (Polish)      → depends on Phase 6
```

### Parallel Opportunities

- All [P] tasks within a phase can run in parallel
- Domain entity creation + tests can be parallelized
- Application DTOs + interfaces are independent files
- EF Core configurations are independent per entity
- Shared UI components with no interdependence

### Subagent-Driven Development

When using `superpowers:subagent-driven-development`:
- Dispatch one subagent per task (fresh context)
- Tasks within the same phase that touch different files: dispatch in parallel
- Tasks across phases: dispatch sequentially (dependency chain)
- Use sonnet for mechanical tasks (enums, DTOs, configs)
- Use opus for design-judgment tasks (pages, complex services)

---

## Notes

- [P] tasks = different files, no dependencies
- [Layer] maps task to architectural layer for constitution traceability
- Verify tests fail before implementing (TDD where applicable)
- Commit after each task or logical group (conventional commits)
- Stop at any checkpoint to validate independently
