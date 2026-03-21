# Milestone 5+ Completion Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development or superpowers:executing-plans.

**Goal:** Complete all remaining milestones to achieve feature parity with A1111/Forge for standard generation workflows.

**Architecture:** Extend existing IInferenceBackend with img2img support, add post-processing pipeline, extend UI with canvas/comparison components.

---

## Phase 1: Core Gaps (P0-P1) — Quick Wins + img2img Foundation

### Task 1: Prompt Token Counter (S)
- Add live token count display next to prompt fields
- Heuristic: tokens ≈ words × 1.3 (77 token CLIP limit for SD1.5, 154 for SDXL)
- Show "42/77" style counter with color warning when approaching limit

### Task 2: Prompt Styles — Append Mode (S)
- Add `ApplyMode` enum (Replace, AppendToPositive, AppendToNegative) to GenerationPresetEntity
- Update PresetSelector to support append mode — clicking a style appends text to current prompt
- Add "Styles" quick-select chip row in workspace

### Task 3: PNG Info Reader — Drag and Drop (M)
- Create `PngInfoParser` that parses A1111-format parameter strings back to structured fields
- Create `PngInfoDropZone.razor` — drag/drop image → read metadata → populate workspace
- "Send to Generate" button that fills all fields from parsed parameters

### Task 4: Advanced Sampler Parameters (S)
- Add to InferenceRequest: Eta (DDIM), denoising strength placeholder, SDXL refiner switch point
- Add "Advanced Sampling" collapsible section in ParameterPanel
- Wire through to StableDiffusionCppBackend

### Task 5: img2img Pipeline (XL)
- Extend InferenceRequest with InitImage (byte[]), MaskImage (byte[]), DenoisingStrength
- Extend IInferenceBackend with img2img path
- Update StableDiffusionCppBackend: use ImageGenerationParameter.ImageToImage()
- Create image upload component with preview
- Create img2img tab in GenerationWorkspace (alongside txt2img)
- Add denoising strength slider (0.0-1.0, default 0.75)

### Task 6: Hires Fix (L)
- Add HiresFixEnabled, HiresUpscaler, HiresSteps, HiresDenoising to GenerationParameters
- Two-pass generation: first pass at lower res, upscale, second pass with img2img
- UI: Hires Fix section in ParameterPanel with upscaler selector

## Phase 2: Creative Tools (P2)

### Task 7: Inpainting (L)
- Canvas component with brush tool for mask painting
- Mask → binary image conversion
- Wire mask to img2img backend
- Inpaint-specific parameters: mask blur, inpaint area (full/masked only)

### Task 8: X/Y/Z Parameter Sweep (L)
- Sweep configuration UI: select X/Y/Z axis parameters
- Batch generation with parameter combinations
- Grid compositor: combine results into a single comparison grid image
- Save grid + individual images

### Task 9: Image Comparison (M)
- Multi-select in gallery (checkbox mode)
- Side-by-side comparison view with overlay slider
- Before/after with draggable divider

### Task 10: Prompt Matrix (S)
- Parse `|` separated prompt segments
- Generate all combinations
- Group results in gallery

### Task 11: LoRA/Embedding Browser with Previews (M)
- Card grid browser for LoRAs and Embeddings
- Fetch preview images from CivitAI metadata
- Show trigger words for LoRAs
- Click to add to current generation
- Search and filter within the browser

## Phase 3: Advanced Features (P3)

### Task 12: ControlNet (XL)
- ControlNet model selection (up to 3 units like Forge)
- Preprocessor integration (Canny, OpenPose, Depth) — via ONNX models
- Weight and guidance range per unit
- Preview of preprocessed image

### Task 13: Face Restoration (L)
- GFPGAN/CodeFormer via ONNX Runtime
- Post-processing toggle in generation settings
- Strength slider

### Task 14: CLIP Interrogation (L)
- Load CLIP model for reverse inference
- "Interrogate" button on any image → generates prompt text
- Both CLIP and DeepBooru modes

### Task 15: Output Configuration (S)
- Custom output directory setting
- Filename pattern (configurable: [seed], [prompt], [date], [model])
- Grid image generation option
- Auto-save toggle

## Phase 4: Infrastructure (P4)

### Task 16: Settings Export/Import (S)
- Export all settings to JSON file
- Import from JSON file
- Settings versioning for forward compatibility

### Task 17: Plugin Architecture (XL)
- Plugin manifest (JSON) with metadata
- Assembly loading and isolation
- Extension points: new samplers, new providers, new UI tabs, post-processors
- Plugin settings page

### Task 18: Model Merging (L)
- Weighted interpolation of two checkpoint models
- UI: select model A, model B, merge ratio, output path
- Merge methods: weighted sum, add difference

---

## Deferred (P5 — Not in Scope)

- Training (Dreambooth/LoRA training) — per SPEC, out of scope
- Multi-user auth — per SPEC, deferred
- Remote worker nodes — per SPEC, deferred
- Outpainting — depends on inpainting, low priority
- Tiling/seamless — backend support unclear
- Color correction — mainly for img2img, add when img2img ships

---

## Execution Order

Phase 1 Tasks 1-4 can be parallelized (all independent quick wins).
Task 5 (img2img) is the critical path — everything in Phase 2 depends on it.
Phase 2 tasks are mostly independent after img2img.
Phase 3 tasks are independent of each other.
Phase 4 is independent infrastructure work.
