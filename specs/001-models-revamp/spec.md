# Feature Specification: Models Page Revamp

**Feature Branch**: `001-models-revamp`
**Created**: 2026-03-21
**Status**: Draft
**Input**: Revamp the models page with unified browsing, rich model cards, side detail drawer, provider improvements (CivitAI/HuggingFace), card/list view toggle, and per-provider download location settings.

## Constitution Alignment

| Principle | How This Feature Complies |
|-----------|--------------------------|
| I. Modular Monolith | New DownloadLocationSettings in Domain. ModelCardViewModel DTO in Application. Provider changes in Infrastructure. UI components in Web. Strict inward dependency. |
| II. Correctness | Provider API error handling with fallbacks. Download failures surface meaningful messages. Missing preview images use placeholders. |
| III. Test-Driven | Domain tests for DownloadLocationSettings. Infrastructure tests for provider mapping logic. E2E smoke test for Models page. |
| IV. UX First | Side drawer preserves scroll position. Real-time download progress. Always-visible filter chips. Empty states with guidance. Help text on download settings. |
| V. Simplicity | Reuses existing MudChipSet, MudDrawer, MudTable. Follows existing provider patterns. No new database tables. |
| VI. Security | Provider API tokens stored locally, transmitted only to provider APIs. Preview images respect NSFW shield. Downloaded files validated by type. |

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse and filter local models (Priority: P1)

A user with 50+ local models wants to quickly find a specific checkpoint by name, filter by family (SDXL vs Flux), and switch between card and list views for different browsing needs.

**Why this priority**: Local model management is the most frequent workflow. Every generation starts with model selection.

**Independent Test**: Load the models page, verify local models display in both card and list views, apply type and family filters, confirm search narrows results.

**Acceptance Scenarios**:

1. **Given** the user has local models scanned, **When** they open the Models page, **Then** they see their local catalog as cards with preview images, family chips, and type badges.
2. **Given** the user has mixed SD 1.5 and SDXL models, **When** they click the "SDXL" family chip, **Then** only SDXL models are shown.
3. **Given** the user has 100+ models, **When** they click the list view toggle, **Then** models display in a dense sortable table with thumbnail, title, family, type, format, and size columns.
4. **Given** the user types "dreamshaper" in search, **When** the debounce fires, **Then** only models containing "dreamshaper" appear.

---

### User Story 2 - Discover and evaluate remote models (Priority: P1)

A user wants to search CivitAI for new SDXL checkpoints, browse results with preview images and descriptions, and evaluate a model's details before downloading.

**Why this priority**: Model discovery is the primary reason users visit the Models page beyond local management.

**Independent Test**: Switch to CivitAI source, search for "realistic", apply SDXL family filter, click a result to open the detail drawer, verify hero image, description, file variants, and download button.

**Acceptance Scenarios**:

1. **Given** the user selects "CivitAI" source chip, **When** they search for "anime", **Then** results display as cards with preview images from CivitAI, family chips, and download counts.
2. **Given** search results are showing, **When** the user clicks the "SDXL" family chip, **Then** results filter to SDXL-base models only.
3. **Given** search results are showing, **When** the user clicks a model card, **Then** a side drawer opens showing: hero image, full description, metadata, trigger words, file variant selector, and download button.
4. **Given** the user switches to "HuggingFace" source chip, **When** they search for "flux", **Then** results show with detected model types, preview images where available, and file variant listings.
5. **Given** the user switches between CivitAI and HuggingFace sources, **When** they return to a previously searched source, **Then** their previous search term, filters, and results are preserved.

---

### User Story 3 - Download models to organized locations (Priority: P2)

A user wants to download a model from CivitAI to a specific folder on their drive, organized by model type, and have it automatically appear in the local catalog.

**Why this priority**: Download organization determines long-term usability. Unorganized downloads create friction.

**Independent Test**: Configure a CivitAI download root in Settings, download a LoRA from CivitAI, verify it lands in the correct type subfolder, and appears in the local catalog.

**Acceptance Scenarios**:

1. **Given** the user opens Settings > Storage Roots, **When** they see the Download Locations section, **Then** they see a card for each remote provider with a download root path field and subfolder preview.
2. **Given** the user sets CivitAI download root to `G:\models\civitai\`, **When** they download a LoRA, **Then** the file is saved to `G:\models\civitai\LoRA\{filename}`.
3. **Given** a download completes, **When** the download root is not already a Storage Root, **Then** it is automatically registered and scanned so the model appears in the local catalog.
4. **Given** the user clicks "Download" in the detail drawer, **When** they select a file variant, **Then** a progress bar appears inline showing download progress, and a success notification shows on completion.

---

### User Story 4 - View model details and take action (Priority: P2)

A user wants to view full details of a local or remote model and take contextual actions like selecting it for generation, sending it to the Parameter Lab, or opening the provider page.

**Why this priority**: Detail views are the decision point between browsing and acting on a model.

**Independent Test**: Click a local model card, verify drawer shows file path, detected date, and action buttons. Click a remote model card, verify drawer shows provider URL and download options.

**Acceptance Scenarios**:

1. **Given** the user clicks a local model card, **When** the drawer opens, **Then** they see: preview image, title, family/type/format chips, action buttons (Select for Generation, Copy File Path, Send to Lab), file path, detected date, and status.
2. **Given** the user clicks "Select for Generation", **Then** they navigate to the Generate page with that model pre-selected.
3. **Given** the user clicks a remote model card, **When** the drawer opens, **Then** they see: hero image, full description, metadata, trigger words (CivitAI), external provider link, and download section with variant selector.

---

### Edge Cases

- What happens when CivitAI API returns an error or is rate-limited? Show error alert, preserve previous results.
- What happens when a model has no preview image? Show a placeholder with the model family icon.
- What happens when HuggingFace repo has no downloadable files? Show "No downloadable files" instead of download button.
- What happens when the download location directory doesn't exist? Create it automatically.
- What happens when a download is interrupted? Show error, allow retry, clean up partial file.
- What happens when switching sources while a download is in progress? Download continues in background, progress visible if drawer reopened.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Page MUST display local models, CivitAI results, and HuggingFace results via source selector chips.
- **FR-002**: Each source MUST maintain independent search/filter state so switching preserves context.
- **FR-003**: Models MUST display in both card grid view and dense list view, toggled by a view button.
- **FR-004**: Card view MUST show preview image, title, family chip (colored by family), type chip, and file size.
- **FR-005**: List view MUST show sortable columns: thumbnail, title, family, type, format, size, source, date.
- **FR-006**: Filter bar MUST show always-visible chips for Model Type and Model Family, with an expandable Advanced section for Sort, NSFW toggle, and tags.
- **FR-007**: Clicking any model MUST open a side drawer with full details without losing scroll position.
- **FR-008**: Detail drawer for local models MUST show metadata and action buttons (Select for Generation, Copy File Path, Send to Lab, Delete from Catalog).
- **FR-009**: Detail drawer for remote models MUST show hero image, full description, file variant selector with sizes/quantization, download button with inline progress, and external provider link.
- **FR-010**: Settings MUST include per-provider download root configuration with subfolder preview.
- **FR-011**: Downloads MUST auto-organize into type subfolders (Checkpoints/, LoRA/, VAE/, Embeddings/, ControlNet/).
- **FR-012**: After download, the download root MUST be auto-registered as a Storage Root and scanned.
- **FR-013**: CivitAI provider MUST support sort order, family filter, NSFW toggle, ControlNet type mapping, trigger words, and all file variants.
- **FR-014**: HuggingFace provider MUST detect model type from tags, detect family, list file variants with sizes, and show preview images.
- **FR-015**: Preview images MUST respect the global NSFW shield toggle.

### Architectural Requirements

- **AR-001**: Domain — `DownloadLocationSettings` value object (immutable sealed record) for per-provider download paths.
- **AR-002**: Application — `ModelCardViewModel` DTO normalizing both `ModelRecordDto` and `RemoteModelInfo`.
- **AR-003**: Infrastructure — Modified `CivitAIProvider` and `HuggingFaceProvider` with enhanced search/mapping logic.
- **AR-004**: Web — Rewritten `Models.razor`, new `ModelCardUnified.razor`, `ModelListRow.razor`, `ModelDetailDrawer.razor`, `ModelFilterBar.razor`. Modified `Settings.razor`.

### Key Entities

- **DownloadLocationSettings**: Immutable sealed record with `Dictionary<string, string> ProviderRoots` mapping provider IDs to download root paths. Provides `GetDownloadPath(providerId, modelType, defaultRoot)` to resolve target directory with type subfolder.
- **ModelCardViewModel**: Positional record normalizing local `ModelRecordDto` and remote `RemoteModelInfo` into unified display fields: Id, Title, PreviewImageUrl, Type, Family, Format, FileSize, Source, IsLocal, IsAvailable, Description.

### Database Requirements

- Tables to create: none (settings stored via existing `ISettingsProvider` key-value store)
- Schema repair SQL needed: no

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can find a specific model within 10 seconds using search and filters.
- **SC-002**: Switching between Local/CivitAI/HuggingFace sources preserves previous search state without re-fetching.
- **SC-003**: Model detail drawer opens without losing scroll position in the results list.
- **SC-004**: Downloaded models appear in the local catalog within 5 seconds of download completion without manual setup.
- **SC-005**: All model cards display preview images (or appropriate placeholders) without layout shift.

### Quality Gate Checklist

- [ ] Builds clean (`dotnet build -c Release`, zero errors)
- [ ] All non-E2E tests pass, test count does not decrease
- [ ] Cross-platform filename/path handling verified
- [ ] Download Locations settings have always-visible help text
- [ ] Download progress shown via inline progress bar
- [ ] Models page has E2E smoke test
- [ ] CivitAI ControlNet type mapping fixed
- [ ] HuggingFace file variants populated
