# ADR-005: Model repositories must be adapter-driven and normalized into a canonical catalog

**Status:** Accepted
**Date:** 2026-03-18

## Context

Users will expect multiple model sources (local directories, Hugging Face, Forge-compatible folders) that behave consistently in the UI.

## Decision

Use source adapters implementing IModelSourceAdapter to normalize metadata into canonical ModelRecord entities in a unified catalog.

## Consequences

### Positive
- Cleaner UI and querying — single model catalog regardless of source
- New sources plug in via adapters without UI changes
- Consistent search, filter, and display across all sources

### Negative
- Need to carefully manage metadata loss or ambiguity across sources
- Each adapter must handle source-specific quirks internally

### Neutral
- Initial adapters: Local Folder, Forge Local, Hugging Face (Milestone 2)
