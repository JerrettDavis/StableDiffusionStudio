# ADR-004: Use pluggable inference backends

**Status:** Accepted
**Date:** 2026-03-18

## Context

The image generation ecosystem changes quickly. Binding to a single execution engine would create vendor lock-in and limit future capabilities.

## Decision

Define stable inference abstractions (IInferenceBackend, IGenerationExecutor, IModelLoader) and support multiple backend implementations. Backend choice is replaceable and does not leak into the UI.

## Consequences

### Positive
- Better long-term survivability and flexibility
- Can support multiple model formats and execution engines
- Users can choose the best backend for their hardware

### Negative
- More architectural work up front
- Abstraction layer adds some complexity

### Neutral
- Initial backend candidates: StableDiffusion.NET, ONNX Runtime, future TorchSharp
- Backend capabilities are declared explicitly for UI compatibility checks
