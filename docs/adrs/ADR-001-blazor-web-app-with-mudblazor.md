# ADR-001: Use a Blazor Web App with MudBlazor for the primary UI

**Status:** Accepted
**Date:** 2026-03-18

## Context

We need a modern .NET-native web-based GUI with fast iteration, strong component reuse, and a cohesive development model for a local-first AI image generation studio.

## Decision

Use ASP.NET Core Blazor Web App with Interactive Server rendering and MudBlazor as the component library.

## Consequences

### Positive
- Strong .NET integration — single language across full stack
- Unified full-stack tooling with C# everywhere
- MudBlazor provides a comprehensive Material Design component library
- Interactive Server mode provides near-zero latency for local-first apps
- SignalR-based real-time updates fit naturally

### Negative
- Potential tradeoffs versus JS-heavy ecosystems for highly custom visual interactions
- Server-side rendering means each user holds a circuit (acceptable for local/small-team use)

### Neutral
- MudBlazor is actively maintained with good community support
- Dark theme support is first-class in MudBlazor
