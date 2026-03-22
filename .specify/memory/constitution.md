<!--
Sync Impact Report
Version change: 0.0.0 → 1.0.0 (initial ratification)
Added principles:
  - I. Modular Monolith Architecture
  - II. Correctness Over Speed
  - III. Test-Driven Quality
  - IV. User Experience First
  - V. Simplicity and YAGNI
  - VI. Security by Default
Added sections:
  - Technology Constraints
  - AI Agent Expectations
  - Quality Gates
  - Development Workflow
  - Governance
Templates requiring updates:
  - .specify/templates/plan-template.md ⚠ pending (align constitution checks)
  - .specify/templates/spec-template.md ⚠ pending (align scope constraints)
  - .specify/templates/tasks-template.md ⚠ pending (align task categories)
Follow-up TODOs: none
-->

# Stable Diffusion Studio Constitution

## Core Principles

### I. Modular Monolith Architecture

Dependencies MUST flow inward: Presentation → Application → Domain.
Infrastructure implements interfaces defined in Application/Domain — never
the reverse. Each layer has strict responsibilities:

- **Domain**: Entities, value objects, enums, domain services. No external
  dependencies. Protects its own state via private setters and factory methods.
- **Application**: Use-case orchestration, DTOs, interfaces, commands/queries.
  Thin services that delegate business logic to Domain.
- **Infrastructure**: EF Core persistence, external APIs, inference backends,
  file I/O. All hidden behind interfaces. Adapters MUST be replaceable.
- **Presentation (Web)**: Blazor components, SignalR hubs, DI registration.
  No business logic — only rendering and user interaction.

New capabilities MUST define interfaces first, then implement adapters.
Cross-layer references that violate the dependency direction are never
acceptable, even "just to make it work."

### II. Correctness Over Speed

When choosing between speed and correctness, choose correctness. When
choosing between cleverness and clarity, choose clarity. When choosing
between a shortcut and the architecture, choose the architecture.

- Code MUST compile with zero errors before committing.
- All tests MUST pass before pushing to main.
- Exceptions MUST NOT be swallowed — use structured error types and
  surface meaningful messages to the UI layer.
- State transitions on domain entities MUST be explicit and
  intention-revealing (e.g., `Start()`, `Complete()`, `Fail(error)`).

### III. Test-Driven Quality

Every feature MUST include tests appropriate to its layer:

- **Domain**: Unit tests for entities, value objects, and domain services.
  Tests enforce invariants, state transitions, and edge cases.
- **Application**: Unit tests for service orchestration with mocked
  dependencies.
- **Infrastructure**: Integration tests for adapters with non-trivial
  behavior (persistence, external APIs, inference).
- **E2E**: Reqnroll + Playwright BDD scenarios for page-level workflows.

Tests MUST be cross-platform — avoid Windows-only assumptions in test
assertions (e.g., use `Split('/', '\\')` instead of `Path.GetFileName`
for path handling). The test suite MUST pass on both Windows and Linux CI.

### IV. User Experience First

The UI MUST be responsive, informative, and never leave users guessing:

- Long-running operations MUST show real-time progress via SignalR — not
  polling, not spinners that disappear prematurely.
- Every setting MUST have always-visible help text explaining what it does,
  why you'd change it, and the trade-offs. No bare toggles.
- Background jobs MUST survive page navigation. The UI is a viewer of
  server state, not the controller.
- Image thumbnails MUST use `object-fit:cover` for consistent aspect
  ratios. Init images MUST be resized to match generation dimensions.
- Empty states MUST be informative with clear guidance on next steps.
- Model loading, generation progress, and errors MUST be communicated
  in real-time with specific phase descriptions.

### V. Simplicity and YAGNI

Only build what is directly requested or clearly necessary:

- Prefer editing existing files over creating new ones.
- Three similar lines of code is better than a premature abstraction.
- Do not add error handling for scenarios that cannot happen.
- Do not design for hypothetical future requirements.
- Do not add features, refactor code, or make "improvements" beyond
  what was asked.
- Follow existing patterns before introducing new ones. If the codebase
  does something a certain way, do it that way unless there's a
  compelling reason not to.

### VI. Security by Default

- Never log secrets or sensitive data.
- Treat all external data (file uploads, API responses, user input)
  as untrusted.
- Validate file inputs and sanitize filenames cross-platform.
- Use secure storage for credentials (provider tokens stored locally,
  never transmitted except to the provider's own API).
- Content safety classification runs locally — no images leave the
  user's machine.

## Technology Constraints

The following technology choices are locked and MUST NOT be changed
without a constitutional amendment:

| Layer | Technology | Version |
|-------|-----------|---------|
| Runtime | .NET | 10 |
| Web Framework | ASP.NET Core Blazor Server | Interactive Server |
| UI Components | MudBlazor | 9.x |
| Database | EF Core + SQLite | Via AppDbContext |
| Real-time | SignalR | StudioHub |
| Inference | StableDiffusion.NET (stable-diffusion.cpp) | CUDA/Vulkan/CPU |
| Content Safety | NsfwSpy | Local ML.NET |
| Testing | xUnit + FluentAssertions + NSubstitute | Latest |
| E2E Testing | Reqnroll + Playwright | Latest |
| Image Interrogation | Ollama (local vision models) | HTTP API |

Infrastructure choices that are implementation details (not locked):
- File storage paths, job queue implementation, PNG metadata format.
- These MAY be changed without amendment if the interface contracts
  are preserved.

## AI Agent Expectations

AI agents working in this repository MUST:

1. **Read before writing.** Never propose changes to code you haven't
   read. Understand existing patterns before suggesting modifications.
2. **Produce complete, runnable output.** No partial implementations,
   placeholder comments like `// TODO: implement`, or stub methods
   unless explicitly requested.
3. **Follow the 16-step workflow** defined in CLAUDE.md Section 16:
   understand → identify layers → define interfaces → implement domain
   → implement application → implement infrastructure → implement UI
   → add tests → update documentation.
4. **Respect layer boundaries.** Never leak infrastructure types into
   domain. Never reference UI from application. Never bypass
   abstractions.
5. **Self-verify.** Build the solution and run tests before claiming
   work is complete. Evidence before assertions.
6. **Use existing components.** Reuse ModelSelector, ParameterPanel,
   ImageUpload, GenerationGallery, and other shared components rather
   than creating duplicates.
7. **Handle the SQLite reality.** Schema changes MUST include
   CREATE TABLE IF NOT EXISTS entries in the startup schema repair
   block. Existing databases MUST upgrade gracefully without data loss.
8. **Commit atomically.** Each commit MUST represent one logical change
   that compiles and passes tests independently.

## Quality Gates

A task is NOT complete until ALL of the following are true:

- [ ] Code compiles with zero errors (`dotnet build -c Release`)
- [ ] All non-E2E tests pass (`dotnet test --filter "!~E2E"`)
- [ ] No regressions — existing test count MUST NOT decrease
- [ ] Cross-platform filename/path handling verified (no `Path.GetFileName`
      on backslash-separated strings that will fail on Linux)
- [ ] Schema changes include CREATE TABLE IF NOT EXISTS in Program.cs
- [ ] New settings have always-visible help text
- [ ] Long-running operations report progress via SignalR
- [ ] New pages have a nav menu entry and an E2E smoke test
- [ ] Conventional commit message format used

## Development Workflow

### Feature Development Process

1. **Brainstorm** — Use the brainstorming skill to explore requirements
   through one-question-at-a-time dialogue. Present 2-3 approaches with
   trade-offs. Validate design in sections.
2. **Specify** — Write a design spec to `docs/specs/YYYY-MM-DD-*.md`.
   Commit the spec before implementation begins.
3. **Plan** — Write a detailed implementation plan to
   `docs/superpowers/plans/YYYY-MM-DD-*.md` with exact file paths,
   code, commands, and commit messages per task.
4. **Implement** — Execute the plan using subagent-driven development.
   Fresh subagent per task. Two-stage review (spec compliance, then
   code quality) after each task.
5. **Verify** — Full build + test suite + push to main.

### Branch Strategy

- Development happens on `main` with atomic commits.
- Feature branches MAY be used for multi-day work requiring isolation.
- All CI workflows (CI, E2E, CodeQL, Docs) MUST pass before considering
  work complete.

### Commit Convention

Format: `type(scope): description`

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `perf`, `chore`
Scopes: `lab`, `gen`, `models`, `settings`, `infra`, or omitted for
cross-cutting changes.

## Governance

This constitution is the supreme governance document for the Stable
Diffusion Studio repository. It supersedes all other guidance except
explicit user instructions in the current conversation.

**Amendment process:**
1. Propose the change with rationale.
2. Assess version impact: MAJOR (principle removal/redefinition),
   MINOR (new principle or material expansion), PATCH (clarification).
3. Update this document and propagate changes to dependent templates.
4. Commit with message: `docs: amend constitution to vX.Y.Z`

**Compliance:**
- All PRs and agent-generated code MUST be verifiable against these
  principles.
- Complexity MUST be justified — if a simpler approach achieves the
  same result, use it.
- CLAUDE.md provides runtime development guidance and is authoritative
  for coding standards. This constitution provides strategic governance.

**Version**: 1.0.0 | **Ratified**: 2026-03-21 | **Last Amended**: 2026-03-21
