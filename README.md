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

- **Project-based workflow** — Organize your work into projects, not scattered folders
- **Model discovery** — Scan local directories, detect model families (SD 1.5, SDXL, Flux), parse metadata
- **Modern dark UI** — MudBlazor-powered interface with a creative-tool aesthetic
- **Background jobs** — Non-blocking model scanning with real-time progress
- **Local-first privacy** — All data stays on your machine, no cloud accounts required
- **Aspire-powered** — Full observability dashboard with OpenTelemetry from day one
- **.NET native** — Built entirely in C# with clean architecture and TDD

### Screenshots

*Coming soon — the app is in active development (Milestone 1+)*

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)

### Run the app

```bash
git clone https://github.com/JerrettDavis/StableDiffusionStudio.git
cd StableDiffusionStudio
dotnet run --project src/StableDiffusionStudio.AppHost
```

This launches the Aspire dashboard. Click the **web** endpoint to open Stable Diffusion Studio.

### Run the tests

```bash
# Unit and integration tests
dotnet test --filter "FullyQualifiedName!~E2E"

# E2E BDD tests (requires Playwright browsers)
pwsh tests/StableDiffusionStudio.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/StableDiffusionStudio.E2E.Tests
```

---

## Architecture

Built as a **modular monolith** with strict layer boundaries:

```
Presentation (Blazor Server + MudBlazor)
    |
Application (Services, Commands, DTOs, Validation)
    |
Domain (Entities, Value Objects, Domain Services)
    ^
Infrastructure (EF Core, File I/O, Model Scanning, Jobs)
```

See [docs/architecture/README.md](docs/architecture/README.md) for the full architecture overview.

### Solution Structure

| Project | Purpose |
|---------|---------|
| `StableDiffusionStudio.Domain` | Entities, value objects, enums, domain services |
| `StableDiffusionStudio.Application` | Use-case orchestration, interfaces, validation |
| `StableDiffusionStudio.Infrastructure` | EF Core, model scanning, background jobs |
| `StableDiffusionStudio.Web` | Blazor Server UI with MudBlazor |
| `StableDiffusionStudio.AppHost` | .NET Aspire orchestration |
| `StableDiffusionStudio.ServiceDefaults` | OpenTelemetry, health checks |

---

## Roadmap

- [x] **Milestone 1+** — App shell, project CRUD, model scanning, background jobs, settings
- [ ] **Milestone 2** — Hugging Face browsing, model downloads, Forge-local scanning
- [ ] **Milestone 3** — txt2img generation pipeline, inference backends, gallery
- [ ] **Milestone 4** — Presets, generation cloning, advanced search, diagnostics
- [ ] **Milestone 5** — img2img, LoRA support, plugin architecture

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

Built with [.NET](https://dotnet.microsoft.com/), [MudBlazor](https://mudblazor.com/), and [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/).
