# Research: Models Page Revamp

## Decision 1: Page Layout — Unified Surface vs Tabs

**Decision**: Unified single-surface with source selector chips (Local, CivitAI, HuggingFace).

**Rationale**: The current tab-per-provider layout shares a single set of state fields across all remote tabs, causing stale results when switching. A unified surface with per-source independent state is cleaner and preserves user context.

**Alternatives considered**:
- Keep tab layout, fix per-tab state — less cohesive, harder to share filter UI
- Separate pages per source — too fragmented, loses cross-source comparison

## Decision 2: Model Detail View — Drawer vs Page vs Dialog

**Decision**: MudDrawer anchored right, ~450px width.

**Rationale**: A side drawer preserves scroll position in the search results list. Users browse results, click to evaluate, then close to continue browsing — all without losing their place. A full page navigation loses scroll context. A modal dialog is too focused and blocks interaction.

**Alternatives considered**:
- Full detail page (`/models/{id}`) — loses scroll position, requires back navigation
- Large modal dialog — blocks results, forces focus, poor for rapid comparison

## Decision 3: Card/List View Toggle

**Decision**: Toggle button switching between card grid and dense MudTable list.

**Rationale**: Card view is best for visual evaluation (preview images, family colors). List view is best for large collections (100+ models) where density matters. Both are standard patterns (Windows Explorer, Finder, A1111 model browser).

**Alternatives considered**:
- Card view only — insufficient for large collections
- List view only — loses visual evaluation capability

## Decision 4: Download Storage Organization

**Decision**: Per-provider configurable download root with automatic type subfolders.

**Rationale**: Users want control over where downloads land (especially when sharing folders with A1111/Forge). Per-provider roots let you point CivitAI downloads to your existing models folder structure. Auto-created type subfolders (Checkpoints/, LoRA/, VAE/) keep things organized without manual sorting.

**Alternatives considered**:
- By type only (no provider separation) — loses provenance tracking
- By provider then type (hardcoded) — not flexible enough for existing folder structures
- Flat downloads folder — creates chaos with 100+ models

## Decision 5: Filter UX — Progressive Disclosure

**Decision**: Always-visible chips for Type and Family. Expandable "Advanced" section for Sort, NSFW, Tags.

**Rationale**: Type and Family are the two filters used in 90%+ of browsing sessions. Sort/NSFW/Tags are power-user features that add clutter when always visible. Progressive disclosure keeps the common case clean.

**Alternatives considered**:
- All filters always visible — too cluttered for casual browsing
- All filters hidden behind "Filter" button — too many clicks for the common case

## Decision 6: HuggingFace Provider Strategy

**Decision**: Enhance the existing provider to fetch file listings, detect types from tags, and construct preview URLs.

**Rationale**: The current HuggingFace provider hardcodes Type=Checkpoint, Format=SafeTensors, and has empty Variants. The HF API supports `?expand[]=siblings` on the search endpoint to get file listings inline, and pipeline tags indicate model type. This can be fixed within the existing provider pattern.

**Alternatives considered**:
- Replace with a new provider implementation — unnecessary, the interface is correct, just the mapping is shallow
- Use HF Hub client library — no .NET client exists, HTTP API is sufficient

## Decision 7: CivitAI Provider Fixes

**Decision**: Fix existing provider in-place — add ControlNet to type map, wire sort/family/NSFW params, extract trigger words, expose all file variants.

**Rationale**: The provider architecture is sound. The gaps are mapping omissions, not design flaws. All fixes are additive — no breaking changes to the interface.

## Technology Research

No new technologies required. All changes use existing dependencies:
- MudDrawer (MudBlazor) — already used elsewhere in the app
- MudChipSet (MudBlazor) — already used for filter chips
- MudTable (MudBlazor) — already used for Jobs page, Settings tables
- ISettingsProvider — already used for inference settings, output settings, interrogation settings
- HttpClient — already used by both providers
