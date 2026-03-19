
# Stable Diffusion Studio

## Comprehensive Product and Design Specification

## 1. Document Status

* **Status:** Draft v1
* **Intended Audience:** Product, architecture, platform, UI/UX, and implementation teams
* **Technology Direction:** .NET 10, ASP.NET Core, Blazor Server/Web App, MudBlazor, .NET Aspire 13+
* **Working Name:** Stable Diffusion Studio

---

## 2. Executive Summary

Stable Diffusion Studio is a local-first, web-based image generation application built on .NET 10. Its purpose is to provide a polished, fast, highly configurable user experience for discovering models, organizing projects, composing prompts, generating images, and iterating on creative workflows without requiring users to operate third-party GUI servers such as Automatic1111 or ComfyUI.

The application should feel as approachable and fluid as modern conversational tools while preserving the depth and control expected by power users of local image generation. A user should be able to install the app, launch it, browse models from multiple sources, create a project, choose a model, configure generation parameters, and begin producing images with minimal friction.

The platform must support a rich future in which image generation is only the first capability. The architecture should anticipate workflows, assets, model families, adapters, pipelines, plugins, and future multimodal capabilities without forcing a rewrite.

---

## 3. Product Vision

### 3.1 Vision Statement

Create the best .NET-native local AI image studio for creators, hobbyists, and developers who want the convenience of modern AI products with the control, privacy, and ownership of running models on their own machine or infrastructure.

### 3.2 Product Thesis

Most local image generation tools are either:

1. extremely powerful but operationally awkward,
2. visually dated or fragmented,
3. heavily dependent on Python and external web UIs,
4. optimized for experts at the expense of discoverability, or
5. difficult to embed into larger .NET-centric systems.

Stable Diffusion Studio should solve this by offering a first-class .NET application with a modern UI, an intuitive project model, built-in model discovery, local model management, and structured generation workflows. The product should be opinionated enough to feel smooth, but flexible enough to satisfy advanced users.

### 3.3 Experience Goals

The app should feel:

* immediate,
* local-first,
* elegant,
* forgiving,
* discoverable,
* project-oriented,
* deeply configurable without looking intimidating,
* extensible for future growth.

---

## 4. Product Goals and Non-Goals

### 4.1 Goals

1. Deliver a polished browser-based GUI built with MudBlazor.
2. Allow users to browse and acquire models from multiple repositories from within the app.
3. Support local repositories and remote repositories through a unified model catalog abstraction.
4. Make project creation extremely fast, similar to starting a new chat session.
5. Enable rich prompt-driven image generation with advanced configuration.
6. Make switching models, prompts, samplers, schedulers, and related settings easy.
7. Provide strong visibility into model files, downloads, storage, metadata, and compatibility.
8. Support local inference backends without requiring a separate third-party GUI process.
9. Build the system on modern .NET architecture with clean boundaries and testability.
10. Prepare the product for future workflows such as inpainting, img2img, LoRA management, batch runs, and automation.

### 4.2 Non-Goals for Initial Release

1. Recreating the full node-graph paradigm of ComfyUI.
2. Building a marketplace or social network.
3. Supporting every possible diffusion backend on day one.
4. Solving distributed multi-machine rendering in the first milestone.
5. Providing a mobile-first UI.
6. Implementing every advanced feature such as ControlNet, video generation, or training in v1.

---

## 5. Target Users

### 5.1 Primary Users

* Local AI enthusiasts who already know Stable Diffusion tools but want a cleaner UX.
* .NET developers who want a programmable, extensible image generation platform.
* Creators who want project-based organization rather than loose folders and disconnected UIs.

### 5.2 Secondary Users

* Teams or hobby groups sharing a workstation or private host.
* Designers exploring model variants and prompt experiments.
* Power users who want a hybrid of convenience and advanced controls.

### 5.3 Personas

#### The Explorer

Wants to browse models, try prompts quickly, and learn by doing. Prefers strong defaults.

#### The Power User

Knows model families, samplers, resolutions, seeds, and adapters. Wants speed and access to knobs.

#### The Builder

Wants to integrate the app into larger .NET systems, automate flows, or extend it with plugins.

---

## 6. Product Principles

1. **Local-first by default.** User assets and models belong to the user.
2. **Fast path first.** Basic generation should take only a few clicks.
3. **Depth without clutter.** Advanced controls should be present but progressively disclosed.
4. **Project-centric workflow.** Work belongs in projects, not scattered state.
5. **Consistent abstractions.** Remote and local model sources should feel unified.
6. **Composable architecture.** UI, catalog, storage, orchestration, and inference must remain decoupled.
7. **Observability built in.** Background downloads, model scans, generation jobs, and failures must be inspectable.
8. **Extensibility by design.** Future features should attach to stable seams.

---

## 7. High-Level Product Scope

### 7.1 Core Capabilities

* User launches app locally or on a private host.
* User sees a dashboard and can create a new project immediately.
* User browses model catalogs from multiple providers.
* User downloads, imports, organizes, and selects models.
* User composes prompts and advanced settings in a generation workspace.
* User submits generation jobs and views outputs in a project gallery.
* User can reuse settings, clone generations, and branch experiments.
* User can manage storage, model locations, caches, downloads, and metadata.

### 7.2 Future Capabilities

* LoRA browser and application.
* Embeddings and textual inversion support.
* Img2img and inpainting.
* ControlNet and conditioning workflows.
* Batch render queues.
* Workflow templates and presets.
* Plugin architecture.
* Multi-user auth and permissions.
* Remote worker nodes and orchestration.

---

## 8. Proposed Architecture Overview

### 8.1 Architectural Style

A modular monolith for the initial product, designed with clear internal boundaries and upgrade paths to distributed services if future needs demand it.

### 8.2 Core Architectural Layers

* **Presentation Layer**

  * Blazor Web App
  * MudBlazor components
  * SignalR-driven live updates for downloads, queues, and progress

* **Application Layer**

  * Use-case orchestration
  * Commands, queries, validation, mapping
  * Project, model, job, and asset workflows

* **Domain Layer**

  * Core entities, value objects, policies, and domain services
  * Generation concepts, model identity, source abstractions, project semantics

* **Infrastructure Layer**

  * Persistence
  * File system access
  * Model repository adapters
  * Inference backend adapters
  * Download services
  * Background processing
  * Observability and caching

### 8.3 Deployment Style

Initial deployment targets:

* local desktop-hosted web app,
* local Docker-hosted app,
* self-hosted private server,
* Aspire-powered dev orchestration for development and integration testing.

---

## 9. Technology Decisions

### 9.1 Primary Stack

* .NET 10
* ASP.NET Core Blazor Web App
* MudBlazor
* .NET Aspire 13+
* EF Core for application persistence
* SQLite for default local installs
* Optional PostgreSQL for advanced/self-hosted deployments
* Background services via ASP.NET Core hosted services
* SignalR for real-time updates
* FluentValidation or equivalent for application-level validation
* OpenTelemetry for diagnostics and tracing

### 9.2 Inference Strategy

The application should not depend on a third-party GUI service such as Automatic1111 or ComfyUI. Instead, inference must be handled through pluggable backend adapters, with one or more local-first backends supported in-process or via tightly owned sidecar execution.

Preferred abstraction:

* `IInferenceBackend`
* `IModelExecutionSession`
* `IGenerationPipeline`

Initial backend candidates:

1. StableDiffusion.NET / stable-diffusion.cpp style backend
2. ONNX Runtime backend where suitable
3. Future TorchSharp-native backend

The architecture must make backend choice replaceable and not leak backend-specific details into the UI.

---

## 10. Functional Requirements

## 10.1 Project Management

The system shall:

1. Allow users to create a new project instantly from the home screen.
2. Allow projects to contain generations, assets, settings snapshots, notes, and model references.
3. Allow users to rename, archive, clone, and delete projects.
4. Allow users to pin recent projects.
5. Allow users to search and filter projects.
6. Allow users to reopen a project and continue where they left off.

### 10.2 Generation Workspace

The system shall provide a generation workspace with:

* positive prompt input,
* negative prompt input,
* model selector,
* sampler selector,
* scheduler selector where applicable,
* width and height controls,
* step count,
* CFG/Guidance scale,
* seed and seed randomization,
* batch size and batch count,
* output format options,
* prompt presets,
* quick reuse of prior configuration,
* advanced panel for expert settings.

The workspace should support cloning a previous generation into the current working state with one action.

### 10.3 Model Browser

The system shall provide a unified model browser capable of:

* browsing remote model repositories,
* browsing local model directories,
* filtering by model type,
* filtering by source,
* searching by name, tags, family, or metadata,
* showing compatibility metadata,
* showing installed/downloaded state,
* initiating model download/import,
* tracking download progress,
* allowing model removal or relocation.

Initial repository integrations should include:

* Hugging Face
* local forge-compatible directories
* manually configured local folders

Future repository integrations may include additional public or private registries.

### 10.4 Model Management

The system shall:

1. Maintain a local catalog of known models.
2. Track origin, metadata, checksum where available, install location, size, and status.
3. Allow users to define one or more model storage roots.
4. Detect models added outside the application through periodic or manual scans.
5. Support model aliases and user-friendly naming.
6. Store compatibility information such as model family, format, backend support, and dependencies.

### 10.5 Downloads and Imports

The system shall:

1. Support background downloads with progress.
2. Allow pause, resume, retry, and cancel where underlying sources permit.
3. Validate integrity when possible.
4. Surface missing auth or gated-model issues clearly.
5. Allow local file import through drag-and-drop or file picker.
6. Allow import from existing Forge or user-specified model directories.

### 10.6 Asset Gallery

The system shall:

1. Show generated outputs in project galleries.
2. Support list and masonry/grid views.
3. Allow image detail viewing with metadata.
4. Allow export, duplicate, delete, and favorite actions.
5. Preserve generation metadata with each asset.
6. Support comparing assets side by side in future iterations.

### 10.7 Presets and Templates

The system shall:

1. Allow saving parameter presets.
2. Allow prompt snippets and reusable prompt templates.
3. Allow project templates in future releases.

### 10.8 Background Jobs

The system shall represent downloads, scans, and generation tasks as jobs with:

* status,
* progress,
* logs,
* timestamps,
* result references,
* error details.

### 10.9 Settings and Configuration

The system shall provide settings for:

* model storage roots,
* cache paths,
* backend preferences,
* download behavior,
* UI preferences,
* performance limits,
* update behavior,
* telemetry toggles,
* security and credential storage.

---

## 11. Non-Functional Requirements

### 11.1 Performance

* The app should launch quickly on a typical developer workstation.
* Common UI actions should feel immediate.
* Model browser queries should return quickly with caching.
* Background work must not freeze the UI.
* Large galleries and model catalogs should support virtualization or incremental loading.

### 11.2 Reliability

* Downloads should survive transient interruptions where feasible.
* Generation jobs should fail transparently and recover gracefully.
* Project and asset metadata should remain consistent across crashes and restarts.

### 11.3 Usability

* New users should be able to go from launch to first generation with minimal instruction.
* Advanced controls should be hidden until requested or grouped in sensible expandable sections.
* Terminology should be accurate but not hostile to newcomers.

### 11.4 Security

* Store secrets such as Hugging Face tokens securely.
* Avoid exposing local file system paths unnecessarily in shared UI surfaces.
* Treat remote metadata and downloads as untrusted input.
* Validate file extensions, structure, and metadata before registration.

### 11.5 Maintainability

* Use modular boundaries and clean contracts.
* Avoid repository-source-specific logic in UI components.
* Centralize configuration and capability discovery.

### 11.6 Extensibility

* New model sources should plug in via adapters.
* New inference backends should plug in via adapters.
* New asset types and workflow types should not require redesigning the app shell.

---

## 12. UX and UI Design Specification

## 12.1 Overall UX Model

The application should combine the conversational immediacy of ChatGPT-style session creation with the precision of a creative workstation.

### UX goals:

* One-click new project creation.
* Clean split between creation flow and management flow.
* Minimal initial clutter.
* High confidence in what model is active and where outputs are going.
* Immediate feedback for downloads and generation jobs.

## 12.2 Primary Screens

### 12.2.1 App Shell

Contains:

* left navigation rail or drawer,
* top command bar,
* search access,
* notification center,
* queue status indicator,
* theme toggle and settings access.

Primary nav sections:

* Home
* Projects
* Generate
* Models
* Downloads / Jobs
* Assets
* Settings

### 12.2.2 Home Dashboard

The dashboard should show:

* quick create project action,
* recent projects,
* recently used models,
* active downloads/jobs,
* storage health summary,
* suggested actions such as “add model” or “resume project”.

### 12.2.3 Project Workspace

A project view should present:

* project title and description,
* generation composer panel,
* active model and presets,
* output gallery,
* project activity/history,
* optional notes/tags.

This should be the heart of the app.

### 12.2.4 Model Browser

The model browser should have:

* source tabs or faceted filters,
* search box,
* cards or rows for models,
* installed state badges,
* metadata quick view,
* download/import actions,
* source-specific detail panel.

### 12.2.5 Job Center

The job center should provide:

* active and completed jobs,
* progress bars,
* resumable actions,
* error details,
* quick navigation to outputs or model entries.

### 12.2.6 Settings

Settings should be grouped by:

* general,
* appearance,
* storage,
* model sources,
* inference backends,
* downloads,
* security,
* diagnostics.

## 12.3 Interaction Patterns

### Primary interaction rules

1. Users should always know which project they are in.
2. Users should always know which model is selected.
3. Advanced settings should be reachable within one interaction, but not always visible.
4. Generation should feel optimistic and progress-driven.
5. Long operations should be non-blocking.
6. Undo-friendly interactions should be favored where practical.

## 12.4 MudBlazor Guidance

Use MudBlazor for:

* app shell and responsive layout,
* drawers, toolbars, dialogs,
* forms, chips, tabs, expansion panels,
* cards for model and asset presentation,
* tables where density is required,
* snackbars for transient feedback,
* progress components for jobs.

Stylistically, the app should aim for a modern dark-first creative-tool feel with a clean neutral palette and restrained accent colors.

---

## 13. Domain Model

## 13.1 Core Entities

### Project

Represents a user-owned workspace containing generations, assets, notes, and configuration context.

### GenerationRequest

Represents the requested parameters for an image generation operation.

### GenerationJob

Represents the execution lifecycle of a generation request.

### Asset

Represents an output image or derivative artifact.

### ModelRecord

Represents a known model in the catalog, whether installed, remote, or imported.

### ModelSource

Represents a repository or source adapter such as Hugging Face or a local directory.

### DownloadJob

Represents acquisition of model or asset content.

### Preset

Represents reusable settings or prompt templates.

## 13.2 Value Objects

* PromptText
* Seed
* Resolution
* SamplerSettings
* FileLocation
* ModelIdentifier
* BackendCapability
* StorageRoot

## 13.3 Relationships

* Project has many GenerationJobs
* Project has many Assets
* Project may reference many ModelRecords over time
* GenerationJob produces one or more Assets
* ModelSource contains many ModelRecords logically
* ModelRecord may be installed in zero or more StorageRoots

---

## 14. Application Services

Recommended application services:

* `ProjectService`
* `GenerationService`
* `ModelCatalogService`
* `ModelImportService`
* `ModelDownloadService`
* `JobOrchestrationService`
* `SettingsService`
* `AssetService`
* `PresetService`

Recommended cross-cutting services:

* `CapabilityRegistry`
* `NotificationService`
* `BackgroundTaskScheduler`
* `SecureSecretStore`
* `FileSystemAbstraction`

---

## 15. Repository Source Architecture

## 15.1 Unified Source Adapter Model

Define an abstraction such as:

* `IModelSourceAdapter`

Responsibilities:

* enumerate models,
* search/filter models,
* fetch metadata,
* resolve download URLs or import details,
* expose source capabilities,
* normalize source-specific metadata into canonical records.

## 15.2 Initial Adapters

### Hugging Face Adapter

Supports browsing repositories, filtering supported model artifacts, metadata caching, gated/private model authentication, and download orchestration.

### Forge Local Adapter

Scans known Forge-compatible directories and normalizes model file metadata.

### Generic Local Folder Adapter

Allows users to register one or more directories to scan and manage.

## 15.3 Metadata Normalization

Canonical fields should include:

* source name,
* external identifier,
* model title,
* model family,
* format,
* size,
* tags,
* description,
* install status,
* compatibility hints,
* preview image if available,
* checksum if available.

---

## 16. Inference Backend Architecture

## 16.1 Abstraction Model

Define backend contracts such as:

* `IInferenceBackend`
* `IInferenceBackendCapabilities`
* `IGenerationExecutor`
* `IModelLoader`

Each backend must declare supported:

* model families,
* formats,
* device types,
* samplers,
* maximum supported dimensions,
* feature support such as txt2img, img2img, inpaint, LoRA.

## 16.2 Backend Selection Rules

The system should:

1. Recommend a backend based on installed capabilities.
2. Warn when a selected model is incompatible with the active backend.
3. Allow the user to override backend choice.
4. Cache backend compatibility results when possible.

---

## 17. Persistence Strategy

## 17.1 Data Categories

### Structured Application Data

Persist in SQLite by default:

* projects,
* job metadata,
* model catalog metadata,
* settings,
* preset definitions,
* audit/history records.

### File-Based Data

Persist in file storage:

* model binaries,
* generated assets,
* previews,
* cache files,
* logs where appropriate.

## 17.2 Storage Layout

Recommended top-level storage concepts:

* `/AppData/Database`
* `/AppData/Models`
* `/AppData/Assets`
* `/AppData/Cache`
* `/AppData/Logs`

Users must be able to configure storage roots.

---

## 18. Background Processing and Real-Time Updates

## 18.1 Background Work Types

* model scanning,
* metadata refresh,
* downloads,
* imports,
* generation jobs,
* cleanup tasks,
* cache eviction.

## 18.2 Orchestration

Background jobs should use durable persisted job records, with live progress updates streamed to the UI via SignalR.

## 18.3 Observability

Every job should have:

* correlation ID,
* timestamps,
* current phase,
* progress data,
* structured error metadata,
* optional raw logs.

---

## 19. Security and Secret Management

### Requirements

* Hugging Face tokens or similar credentials must be stored using OS-appropriate secure storage where feasible.
* Secrets must never be logged in plaintext.
* Downloads from untrusted origins must be validated before registration.
* The app should surface trust boundaries clearly when importing arbitrary files.

### Initial Approach

* local secret abstraction,
* user-scoped secrets where possible,
* environment variable fallback for headless/self-hosted scenarios.

---

## 20. Diagnostics and Telemetry

The application should support:

* structured logging,
* OpenTelemetry traces,
* metrics for downloads, generation duration, failures, and cache behavior,
* optional local-only telemetry mode by default.

Diagnostics should primarily serve the user and the developer running the system, not a SaaS backend.

---

## 21. Testing Strategy

## 21.1 Test Layers

* domain unit tests,
* application service tests,
* adapter contract tests,
* infrastructure integration tests,
* Blazor component tests,
* end-to-end UI tests,
* performance smoke tests.

## 21.2 Critical Test Focus Areas

* model source normalization,
* project lifecycle,
* generation request validation,
* backend compatibility rules,
* download resume/retry behavior,
* persistence consistency,
* background job recovery.

---

## 22. Proposed Repository Structure

```text
/StableDiffusionStudio.sln
/docs
  /adrs
  /architecture
  /product
  /specs
/src
  /StableDiffusionStudio.AppHost
  /StableDiffusionStudio.ServiceDefaults
  /StableDiffusionStudio.Web
  /StableDiffusionStudio.Application
  /StableDiffusionStudio.Domain
  /StableDiffusionStudio.Infrastructure
  /StableDiffusionStudio.Persistence
  /StableDiffusionStudio.Inference.Abstractions
  /StableDiffusionStudio.Inference.StableDiffusionCpp
  /StableDiffusionStudio.Inference.Onnx
  /StableDiffusionStudio.ModelSources.Abstractions
  /StableDiffusionStudio.ModelSources.HuggingFace
  /StableDiffusionStudio.ModelSources.ForgeLocal
  /StableDiffusionStudio.ModelSources.LocalFolder
/tests
  /StableDiffusionStudio.Domain.Tests
  /StableDiffusionStudio.Application.Tests
  /StableDiffusionStudio.Infrastructure.Tests
  /StableDiffusionStudio.Web.Tests
  /StableDiffusionStudio.E2E.Tests
```

---

## 23. Initial ADR Set

## ADR-001: Use a Blazor Web App with MudBlazor for the primary UI

**Status:** Accepted

### Context

We need a modern .NET-native web-based GUI with fast iteration, strong component reuse, and a cohesive development model.

### Decision

Use ASP.NET Core Blazor Web App with MudBlazor.

### Consequences

* Strong .NET integration
* Unified full-stack language and tooling
* Potential tradeoffs versus JS-heavy ecosystems for some highly custom visual interactions

## ADR-002: Start as a modular monolith

**Status:** Accepted

### Context

The product needs strong internal boundaries but does not initially justify distributed complexity.

### Decision

Build as a modular monolith with explicit layer and module boundaries.

### Consequences

* Faster delivery
* Easier local debugging
* Clear migration path later if remote workers or services become necessary

## ADR-003: Use SQLite as default persistence

**Status:** Accepted

### Context

The default install must work locally without requiring separate infrastructure.

### Decision

Use SQLite by default and support PostgreSQL later for advanced deployments.

### Consequences

* Simplified onboarding
* Some future migration concerns for high-scale multi-user scenarios

## ADR-004: Use pluggable inference backends instead of binding the app to one execution engine

**Status:** Accepted

### Context

The image generation ecosystem changes quickly. We need adaptability.

### Decision

Define stable inference abstractions and support multiple backend implementations.

### Consequences

* More architectural work up front
* Better long-term survivability and flexibility

## ADR-005: Model repositories must be adapter-driven and normalized into a canonical catalog

**Status:** Accepted

### Context

Users will expect multiple model sources that behave consistently.

### Decision

Use source adapters and normalize metadata into canonical model records.

### Consequences

* Cleaner UI and querying
* Need to carefully manage metadata loss or ambiguity across sources

## ADR-006: Prioritize local-first privacy and ownership

**Status:** Accepted

### Context

This product exists largely to keep control local.

### Decision

Store data locally by default and avoid requiring a cloud account.

### Consequences

* Stronger privacy posture
* More responsibility for local storage and lifecycle management

---

## 24. Milestone Plan

## Milestone 1: Foundational Shell

* app shell,
* project creation,
* local persistence,
* settings,
* background job framework,
* basic model catalog abstractions.

## Milestone 2: Model Browser and Local Management

* Hugging Face browsing,
* local folder scanning,
* forge-local scanning,
* download/import,
* catalog UI.

## Milestone 3: First Generation Pipeline

* txt2img workflow,
* backend integration,
* generation jobs,
* gallery and metadata viewing.

## Milestone 4: Quality and Power Features

* presets,
* generation cloning,
* richer job center,
* better filtering/search,
* compatibility diagnostics.

## Milestone 5: Advanced Extensions

* img2img,
* LoRA support,
* plugin seams,
* project templates,
* deeper Aspire and deployment support.

---

## 25. Open Questions

1. Should the first shipping mode be Blazor Server, Blazor Web App with interactive server components, or a hybrid mode?
2. Which inference backend should be the canonical v1 backend?
3. What exact set of model families and formats are required in v1?
4. How much metadata should be cached from remote repositories locally?
5. Should the app support multi-user auth in the first hosted release, or remain single-user-first?
6. Should asset storage be fully content-addressed from the beginning?
7. How aggressively should we optimize for offline remote-repository browsing from cached metadata?

---

## 26. Recommended Initial Decisions

To unblock implementation, this spec recommends the following initial calls:

* Use **Blazor Web App with interactive server components** for v1.
* Use **MudBlazor** for all primary UI components.
* Use **SQLite** for default local persistence.
* Use **SignalR** for live progress and job updates.
* Start with **Hugging Face**, **Forge Local**, and **generic local folders** as model sources.
* Start with **txt2img** as the first supported generation mode.
* Treat **StableDiffusion.NET / stable-diffusion.cpp-style integration** as the default first backend candidate.
* Build the codebase as a **modular monolith** with strong abstractions around model sources and inference backends.

---

## 27. Definition of Success

The first meaningful release is successful if a user can:

1. launch the app locally,
2. create a project in seconds,
3. browse or import a model from within the app,
4. select the model,
5. enter a prompt and generation settings,
6. generate one or more images,
7. review those outputs in a polished project gallery,
8. return later and continue seamlessly.

If those actions feel smooth, coherent, and modern, the product is on the right path.
