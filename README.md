# Stable Diffusion Studio

> **The best .NET-native local AI image studio**
> Create, organize, and generate AI images with the control and privacy of running models on your own machine.

[![CI](https://github.com/JerrettDavis/StableDiffusionStudio/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/StableDiffusionStudio/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JerrettDavis/StableDiffusionStudio/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/JerrettDavis/StableDiffusionStudio/security/code-scanning)
[![codecov](https://codecov.io/gh/JerrettDavis/StableDiffusionStudio/graph/badge.svg)](https://codecov.io/gh/JerrettDavis/StableDiffusionStudio)
[![License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](LICENSE)

---

## Overview

Stable Diffusion Studio is a local-first, web-based image generation application built on .NET 10. It provides a polished, fast, and highly configurable user experience for discovering models, organizing projects, composing prompts, generating images, and iterating on creative workflows — without requiring third-party GUI servers like Automatic1111 or ComfyUI.

### Key Features

- **Image generation** — txt2img, img2img, and inpainting with SD 1.5, SDXL, and Flux models via StableDiffusion.NET (CUDA + Vulkan GPU acceleration)
- **Model management** — Browse and download from HuggingFace and CivitAI, scan local folders, auto-detect model types (Checkpoint, VAE, LoRA, Embedding, ControlNet)
- **A1111-style workspace** — Model/VAE/LoRA selection, sampler/scheduler/steps/CFG/seed controls, CLIP skip, batch generation, resolution presets
- **Project-based workflow** — Organize work into projects with persistent generation history
- **Presets system** — Save/load generation parameter presets, filtered by model and family
- **Prompt tools** — Prompt history with search, negative prompt quick-insert tags, Ctrl+Enter to generate
- **Image gallery** — Favorites, NSFW shield with configurable threshold, per-image actions (copy seed/prompt, delete)
- **Content safety** — Fully-local NSFW detection via NsfwSpy ML.NET, blur/reveal/block modes, A1111-compatible PNG metadata embedding
- **Live preview** — Skeleton progress card with step counter during generation, SignalR real-time updates
- **Background jobs** — Non-blocking generation/downloads with progress tracking, cancellation, interactive job detail view
- **Performance settings** — VRAM presets, thread count, flash attention, VAE tiling, CPU/GPU offloading controls
- **Warm industrial design** — Custom dark/light theme with Inter + JetBrains Mono typography
- **Local-first privacy** — All data stays on your machine, no cloud accounts required
- **Aspire-powered** — Full observability dashboard with OpenTelemetry

### GPU Support

| Backend | Status | Models |
|---------|--------|--------|
| **CUDA 12** (NVIDIA) | Bundled via NuGet | SD 1.5, SDXL |
| **Vulkan** (NVIDIA/AMD) | Bundled via NuGet | SD 1.5, SDXL, Flux |
| **CPU** (AVX2) | Bundled via NuGet | All (slow) |

CUDA is auto-selected for NVIDIA GPUs. Vulkan is the fallback for universal GPU support. CPU offloading is automatically disabled on CUDA to prevent AVX512 crashes on hybrid Intel CPUs.

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- NVIDIA GPU recommended (RTX 3000+ for best performance)

### Run the app

```bash
git clone https://github.com/JerrettDavis/StableDiffusionStudio.git
cd StableDiffusionStudio

# Direct launch (recommended — fastest startup, most stable)
dotnet run --project src/StableDiffusionStudio.Web
# Open http://localhost:5190

# Or with Aspire dashboard (adds OpenTelemetry monitoring)
dotnet run --project src/StableDiffusionStudio.AppHost
```

### First steps

1. Go to **Settings → Storage Roots** and add your model directory (e.g., `G:\sd.webui\webui\models\Stable-diffusion`)
2. Tag additional folders for LoRA, VAE, etc.
3. Go to **Models** and click **Scan Now**
4. Go to **Generate** or create a **Project**, select a model, enter a prompt, and generate

### Run the tests

```bash
# Unit and integration tests (819+)
dotnet test --filter "FullyQualifiedName!~E2E"

# E2E BDD tests (Playwright + Reqnroll)
pwsh tests/StableDiffusionStudio.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/StableDiffusionStudio.E2E.Tests
```

---

## Architecture

Built as a **modular monolith** with strict layer boundaries:

```
Presentation (Blazor Server + MudBlazor + SignalR)
    ↓
Application (Services, Commands, DTOs, Validation)
    ↓
Domain (Entities, Value Objects, Domain Services)
    ↑
Infrastructure (EF Core + SQLite, StableDiffusion.NET, NsfwSpy, File I/O)
```

See [docs/architecture/README.md](docs/architecture/README.md) for the full architecture overview.

### Solution Structure

| Project | Purpose |
|---------|---------|
| `StableDiffusionStudio.Domain` | Entities, value objects, enums, domain services |
| `StableDiffusionStudio.Application` | Use-case orchestration, interfaces, validation |
| `StableDiffusionStudio.Infrastructure` | EF Core, inference backends, model providers, background jobs |
| `StableDiffusionStudio.Web` | Blazor Server UI with MudBlazor, SignalR hubs |
| `StableDiffusionStudio.AppHost` | .NET Aspire orchestration |
| `StableDiffusionStudio.ServiceDefaults` | OpenTelemetry, health checks |

---

## Milestones

- [x] **Milestone 1+** — App shell, project CRUD, model scanning, background jobs, settings, dark theme
- [x] **Milestone 2** — Unified model provider system (HuggingFace, CivitAI), model downloads, tagged model folders, model type classification
- [x] **Milestone 3** — Generation pipeline (txt2img), StableDiffusion.NET inference backend (CUDA + Vulkan), generation workspace with A1111-style controls, gallery with metadata
- [x] **Milestone 4** — Presets, prompt history, image favorites, PNG metadata embedding, negative prompt helpers, advanced parameters (CLIP skip, batch count), data management
- [x] **Milestone 5** — img2img, inpainting, plugin architecture (ControlNet deferred — requires preprocessor models)

### What's New

- **img2img** — Upload an init image, adjust denoising strength, generate variations
- **Inpainting** — Paint a mask over an init image to selectively regenerate regions (canvas with configurable brush, touch support)
- **Settings export/import** — Export all settings to JSON, import from file to restore configuration
- **Plugin architecture** — IPlugin, IPostProcessor, IModelProviderPlugin interfaces with assembly-scanning PluginManager

### Additional features built

- Content safety (NSFW detection + shield toggle)
- Live generation preview (skeleton card with step progress)
- Backend performance settings (VRAM presets, offloading controls)
- Custom design system (warm industrial palette, Inter + JetBrains Mono)
- Flux model support (auto-detect VAE, CLIP-L, T5-XXL components)
- Background generation (survives page navigation)
- Interactive Jobs page with detail view
- Keyboard shortcuts (Ctrl+Enter)
- Recent models quick-switch
- Database health check and diagnostics
- Robust schema migration and repair

---

## Contributing

Contributions are welcome! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create a feature branch (`git checkout -b feat/amazing-feature`)
3. Commit your changes using [Conventional Commits](https://www.conventionalcommits.org/)
4. Push to the branch (`git push origin feat/amazing-feature`)
5. Open a Pull Request

---

## Documentation

- [Architecture Overview](docs/architecture/README.md)
- [Product Specification](docs/SPEC.md)
- [ADR Decision Records](docs/adrs/)

---

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

Built with [.NET](https://dotnet.microsoft.com/), [MudBlazor](https://mudblazor.com/), [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/), [StableDiffusion.NET](https://github.com/DarthAffe/StableDiffusion.NET), and [NsfwSpy](https://github.com/d00ML0rDz/NsfwSpy).
