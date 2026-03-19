# ADR-002: Start as a modular monolith

**Status:** Accepted
**Date:** 2026-03-18

## Context

The product needs strong internal boundaries but does not initially justify distributed complexity. The primary deployment target is a local desktop or self-hosted server.

## Decision

Build as a modular monolith with explicit layer and module boundaries: Domain, Application, Infrastructure, and Presentation layers with strict dependency direction.

## Consequences

### Positive
- Faster delivery — single deployable unit
- Easier local debugging — no distributed tracing complexity
- Clear migration path to services if future scale demands it

### Negative
- Must maintain discipline around module boundaries without physical process separation
- Risk of boundary erosion over time without code review discipline

### Neutral
- .NET Aspire provides orchestration tooling even within a monolith
