# ADR-006: Prioritize local-first privacy and ownership

**Status:** Accepted
**Date:** 2026-03-18

## Context

This product exists largely to keep control of AI generation local. Users choose local tools specifically to avoid cloud dependencies and data sharing.

## Decision

Store all data locally by default. Never require a cloud account for core functionality. Telemetry is local-only by default.

## Consequences

### Positive
- Stronger privacy posture — user data never leaves their machine
- No cloud account requirement reduces onboarding friction
- Full offline capability for core workflows

### Negative
- More responsibility for local storage lifecycle management
- No cloud-based backup or sync in default mode

### Neutral
- Optional cloud features (remote model registries) can be added without changing the core privacy model
