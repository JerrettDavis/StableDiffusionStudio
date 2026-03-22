---
description: "Task list for Models Page Revamp feature implementation"
---

# Tasks: Models Page Revamp

**Input**: Design documents from `/specs/001-models-revamp/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Constitution**: All tasks comply with `.specify/memory/constitution.md`.
Key enforcement points marked with markers below.

**Organization**: Tasks grouped by architectural layer (Domain → Application →
Infrastructure → Web), then by user story for UI components.

## Format: `[ID] [P?] [Layer/Story] Description`

---

## Phase 1: Domain Layer

**Purpose**: New value object for download location settings.

- [ ] T001 [P] [DOM] Create DownloadLocationSettings value object in src/StableDiffusionStudio.Domain/ValueObjects/DownloadLocationSettings.cs — sealed record with Dictionary<string, string> ProviderRoots, GetDownloadPath(providerId, modelType, defaultRoot) method with type subfolder mapping, static Default property
- [ ] T002 [P] [DOM] Write unit tests for DownloadLocationSettings in tests/StableDiffusionStudio.Domain.Tests/ValueObjects/DownloadLocationSettingsTests.cs — test GetDownloadPath returns correct subfolder per ModelType, test default root fallback when provider not configured, test custom root override, test all 5 type subfolder mappings plus "Other" fallback

**Checkpoint**: Domain compiles, domain tests pass.

```bash
dotnet test tests/StableDiffusionStudio.Domain.Tests --filter "DownloadLocation" --no-restore -v quiet
```

---

## Phase 2: Application Layer

**Purpose**: Unified view model DTO for model cards.

- [ ] T003 [P] [APP] Create ModelCardViewModel record in src/StableDiffusionStudio.Application/DTOs/ModelCardViewModel.cs — positional record with Id, Title, PreviewImageUrl, Type, Family, Format, FileSize, Source, IsLocal, IsAvailable, Description. Static factory methods FromLocal(ModelRecordDto, IAppPaths) and FromRemote(RemoteModelInfo, string providerId)

**Checkpoint**: Application compiles.

```bash
dotnet build src/StableDiffusionStudio.Application --no-restore
```

---

## Phase 3: Infrastructure — Provider Improvements

**Purpose**: Fix CivitAI gaps, overhaul HuggingFace provider.

- [ ] T004 [P] [INF] Fix CivitAIProvider in src/StableDiffusionStudio.Infrastructure/ModelSources/CivitAIProvider.cs — add ControlNet to CivitTypeMap, map ModelSearchQuery.Sort to API sort param (Relevance/MostDownloaded→"Most Downloaded", Newest→"Newest", HighestRated→"Highest Rated"), pass baseModels query param from Family filter (SD15→"SD 1.5", SDXL→"SDXL 1.0", Flux→"Flux.1 D,Flux.1 S", Pony→"Pony"), pass nsfw param based on content safety filter mode, extract trainedWords from modelVersions[0] into Tags, expose all file variants from files[] not just first, set ProviderUrl to https://civitai.com/models/{modelId}
- [ ] T005 [P] [INF] Overhaul HuggingFaceProvider in src/StableDiffusionStudio.Infrastructure/ModelSources/HuggingFaceProvider.cs — use ?expand[]=siblings on search endpoint to get file listing, detect model type from pipeline tags (text-to-image→Checkpoint, lora tag→LoRA, embedding tag→Embedding), detect family from tags (stable-diffusion→SD15, sdxl→SDXL, flux→Flux), populate PreviewImageUrl from card thumbnail or resolve URL, list .safetensors and .gguf files from siblings as Variants with sizes, set ProviderUrl to https://huggingface.co/{id}, fix download URL to use actual filenames from file listing
- [ ] T006 [P] [INF] Add/update provider mapping tests in tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/ — test CivitAI ControlNet type mapping, sort param mapping, family filter mapping, trained words extraction. Test HuggingFace type detection from tags, family detection, file variant listing

**Checkpoint**: Infrastructure compiles, provider tests pass.

```bash
dotnet test tests/StableDiffusionStudio.Infrastructure.Tests --filter "Provider" --no-restore -v quiet
```

---

## Phase 4: Web — Shared UI Components

**Purpose**: Reusable components for the revamped models page.

- [ ] T007 [P] [WEB] Create ModelFilterBar component in src/StableDiffusionStudio.Web/Components/Shared/ModelFilterBar.razor — full-width search input (debounced 300ms), view toggle button (grid/list icons), source selector MudChipSet (Local/CivitAI/HuggingFace), always-visible chips for ModelType and ModelFamily (colored: blue=SD1.5, purple=SDXL, orange=Flux, pink=Pony), expandable Advanced section with MudSelect for SortOrder, NSFW MudSwitch, tag MudTextField. Parameters: SearchTerm (two-way), SelectedSource (two-way), TypeFilter, FamilyFilter, SortOrder, NsfwEnabled, ViewMode (two-way), OnSearchRequested EventCallback
- [ ] T008 [P] [WEB] Create ModelCardUnified component in src/StableDiffusionStudio.Web/Components/Shared/ModelCardUnified.razor — accepts ModelCardViewModel, renders: hero image (aspect-ratio 4:3, object-fit:cover, placeholder with family icon if no image), title (1 line ellipsis), family chip (colored) + type chip (subtle), footer with file size and source icon. Click fires OnClick EventCallback. NSFW images respect CascadingParameter shield. Local models show green/red status dot
- [ ] T009 [P] [WEB] Create ModelListRow component for list view — used inside MudTable, shows: tiny 40px thumbnail, Title, Family chip, Type chip, Format, Size, Source icon, Date. Click fires OnClick EventCallback
- [ ] T010 [P] [WEB] Create ModelDetailDrawer component in src/StableDiffusionStudio.Web/Components/Shared/ModelDetailDrawer.razor — MudDrawer anchored right ~450px. Accepts ModelCardViewModel plus RemoteModelInfo (for remote details) plus ModelRecordDto (for local details). Hero section: preview image full width max-height 300px, title h5, family/type/format chips, source badge. Local actions: Select for Generation (navigate /generate), Copy File Path, Send to Lab (navigate /lab), Delete from Catalog. Remote actions: variant MudSelect (filename/size/quantization), Download button with inline progress bar, View on Provider external link. Details section: full description (HTML for CivitAI), metadata table, trigger words, file path (local), dates

**Checkpoint**: Web compiles.

```bash
dotnet build src/StableDiffusionStudio.Web --no-restore -c Release
```

---

## Phase 5: Web — Models Page Rewrite (US1 + US2)

**Purpose**: Unified models page covering User Stories 1 (local browsing) and 2 (remote discovery).

- [ ] T011 [US1+US2] Rewrite Models.razor page in src/StableDiffusionStudio.Web/Components/Pages/Models.razor — replace current tab layout with unified layout: ModelFilterBar at top, results area below. Per-source independent state using Dictionary<string, SourceState> where SourceState holds SearchTerm, TypeFilter, FamilyFilter, SortOrder, Results list, Page, HasMore, IsLoading. Switching sources restores previous state without re-fetching. Card grid view (default) using ModelCardUnified in MudGrid, or dense MudTable using ModelListRow when list view selected. Load More button for remote sources. Click any model to open ModelDetailDrawer (right-side MudDrawer). Local source: load from IModelCatalogService.ListAsync with client-side filtering. Remote sources: call IModelCatalogService.SearchAsync with all filter params. Keep existing Scan and Add Directory buttons for local source

**Checkpoint**: Models page loads, local browsing works, remote search works.

---

## Phase 6: Web — Download Locations (US3)

**Purpose**: Per-provider download root settings and auto-organization.

- [ ] T012 [US3] Add Download Locations section to Settings page in src/StableDiffusionStudio.Web/Components/Pages/Settings.razor — new section in Storage Roots tab below existing root list. Per-provider card (CivitAI, HuggingFace) showing: provider name, download root path MudTextField with help text, preview of auto-created subfolders (Checkpoints/, LoRA/, VAE/, Embeddings/, ControlNet/), Save button. Load/save via ISettingsProvider with key "DownloadLocations" as DownloadLocationSettings
- [ ] T013 [US3] Wire download flow in ModelDetailDrawer — when user clicks Download: resolve path via DownloadLocationSettings.GetDownloadPath, call IModelCatalogService.RequestDownloadAsync, show inline progress bar. After download completes: auto-register download root as Storage Root via IStorageRootProvider if not already registered, trigger scan of that root

**Checkpoint**: Download settings save/load, downloads land in correct folders.

---

## Phase 7: Web — Detail Actions (US4)

**Purpose**: Contextual actions from model detail drawer.

- [ ] T014 [US4] Wire drawer action buttons — Select for Generation: navigate to /generate with model ID as query param. Copy File Path: clipboard copy via JS interop. Send to Lab: navigate to /lab with model as checkpoint. Delete from Catalog: confirmation dialog then call IDataManagementService. View on Provider: MudLink with external URL from ProviderUrl

**Checkpoint**: All drawer actions functional.

---

## Phase 8: Polish & Verification

**Purpose**: Final quality pass.

- [ ] T015 Add E2E scenario for Models page in tests/StableDiffusionStudio.E2E.Tests/Features/FullWorkflow.feature — scenario: navigate to models page, verify heading visible, page has no errors
- [ ] T016 Run full build and test suite to verify no regressions

```bash
dotnet build --no-restore -c Release
dotnet test --filter "FullyQualifiedName!~E2E" --no-restore -v quiet
```

- [ ] T017 Commit and push to main

---

## Dependencies & Execution Order

### Layer Dependencies (Constitution Principle I)

```
Phase 1 (Domain)        → no dependencies
Phase 2 (Application)   → depends on Phase 1
Phase 3 (Infrastructure) → no dependency on Phase 1-2 (modifies existing files)
Phase 4 (UI Components)  → depends on Phase 2 (uses ModelCardViewModel)
Phase 5 (Models Page)    → depends on Phases 3 + 4
Phase 6 (Download Settings) → depends on Phase 1 + 5
Phase 7 (Detail Actions) → depends on Phase 5
Phase 8 (Polish)         → depends on all above
```

### Parallel Opportunities

- T001 + T002 (domain VO + tests) can run in parallel
- T004 + T005 + T006 (CivitAI fix + HF overhaul + tests) can run in parallel
- T007 + T008 + T009 + T010 (all UI components) can run in parallel
- T012 + T014 (settings + actions) can run in parallel after T011

### Subagent-Driven Development

When using `superpowers:subagent-driven-development`:
- **Sonnet**: T001-T003 (domain VO, DTO — mechanical)
- **Sonnet**: T006 (provider tests — mechanical)
- **Sonnet**: T012, T015 (settings section, E2E — mechanical)
- **Opus**: T004, T005 (provider overhauls — design judgment)
- **Opus**: T007-T011 (UI components + page rewrite — design judgment)
- **Opus**: T010, T013 (drawer + download flow — integration complexity)

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1-2: Domain + Application
2. Complete Phase 3: Provider improvements
3. Complete Phase 4: UI components
4. Complete Phase 5: Models page rewrite
5. **STOP and VALIDATE**: Local browsing + remote search work independently

### Incremental Delivery

1. Phases 1-5 → MVP: browse + search + evaluate (US1 + US2)
2. Phase 6 → Add: organized downloads (US3)
3. Phase 7 → Add: detail actions (US4)
4. Phase 8 → Polish: E2E tests, final verification

---

## Notes

- [P] tasks = different files, no dependencies
- [Layer/Story] maps task to architectural layer or user story
- Total: 17 tasks across 8 phases
- No new database tables — settings via existing ISettingsProvider
- Existing Models page is fully replaced (not incrementally modified)
