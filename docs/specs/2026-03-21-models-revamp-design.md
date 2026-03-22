# Models Page Revamp — Design Specification

## Overview

Complete overhaul of the Models page to provide a unified, polished experience for discovering, evaluating, and managing both local and remote models. Replaces the current tab-per-provider layout with a single-surface design featuring source chips, rich filter bar, card/list view toggle, and a side drawer for model details. Includes provider improvements (CivitAI fixes, HuggingFace overhaul) and configurable per-provider download locations.

## Goals

- Unified browsing across local catalog, CivitAI, and HuggingFace with per-source state preservation
- Rich model cards with preview images, family chips, and type indicators
- Side drawer for full model details without losing scroll position
- Card grid and dense list view toggle for different collection sizes
- Smart defaults with progressive disclosure for filters (type/family chips visible, sort/tags/NSFW in Advanced)
- Per-provider download root configuration with auto-organization by model type
- Fix CivitAI provider gaps (ControlNet type, sort, family filter, NSFW)
- Overhaul HuggingFace provider (file listing, type detection, preview images)

## Page Structure

### Layout

Single page at `/models` with three horizontal zones:

1. **Top bar** — Full-width search input (debounced, 300ms), view toggle button (grid/list icons), source selector (MudChipSet: "Local", "CivitAI", "HuggingFace")
2. **Filter bar** — Always-visible chips for Model Type and Model Family. Expandable "Advanced" section for Sort, NSFW toggle, tag input.
3. **Results area** — Card grid (default) or dense table, with "Load More" for remote sources.

### Per-Source State

Each source maintains independent state so switching between them preserves context:

```
Dictionary<string, SourceState> where SourceState = {
    SearchTerm, TypeFilter, FamilyFilter, SortOrder,
    Results, Page, HasMore, IsLoading
}
```

Switching sources restores the previous search term, filters, and results without re-fetching.

### Source-Specific Filter Behavior

| Filter | Local | CivitAI | HuggingFace |
|--------|-------|---------|-------------|
| Search | Client-side filter on title | API search | API search |
| Type | Client-side filter | API `types` param | Inferred from tags |
| Family | Client-side filter | API `baseModels` param | Inferred from tags |
| Sort | Name, Date Added, Size | Most Downloaded, Newest, Highest Rated | Most Downloads, Most Likes |
| NSFW | N/A (uses content safety) | API `nsfw` param | N/A |
| Tags | Client-side filter on tags | API `tag` param | API `search` combined |

## Model Cards

### Unified Card Component (ModelCardUnified.razor)

Accepts a `ModelCardViewModel` that normalizes both `ModelRecordDto` and `RemoteModelInfo`:

```csharp
public record ModelCardViewModel(
    string Id,              // Guid.ToString() for local, ExternalId for remote
    string Title,
    string? PreviewImageUrl,
    ModelType Type,
    ModelFamily Family,
    ModelFormat Format,
    long? FileSize,
    string Source,          // "local", "civitai", "huggingface"
    bool IsLocal,
    bool IsAvailable,       // false if local model marked Missing
    string? Description);
```

**Card layout:**
- Hero image (aspect-ratio ~4:3, object-fit:cover, placeholder with family icon if no image)
- Title (1 line, ellipsis overflow)
- Chips: family (colored — blue=SD1.5, purple=SDXL, orange=Flux, pink=Pony) + type (subtle)
- Footer: file size left, source icon right
- Click opens ModelDetailDrawer

**NSFW handling:** Preview images respect the global shield toggle via CascadingParameter.

### List View (ModelListRow.razor)

Dense MudTable row with columns: tiny thumbnail (40px square), Title, Family chip, Type chip, Format, Size, Source icon, Date. Sortable. Click opens drawer.

### View Toggle

Icon button in the top bar toggles between `ViewModule` (grid) and `ViewList` (list). Preference persisted in component state (not database — it's a UI-only preference).

## Model Detail Drawer (ModelDetailDrawer.razor)

MudDrawer anchored right, ~450px width, opens on model click.

### Hero Section
- Preview image (full width, max-height 300px, object-fit:cover)
- Title as h5
- Family + Type + Format chips
- Source badge

### Action Buttons
**Local models:**
- "Select for Generation" — navigates to /generate with model pre-selected
- "Copy File Path" — clipboard copy
- "Send to Parameter Lab" — navigates to /lab with model as checkpoint
- "Delete from Catalog" — confirmation dialog, removes record (not file)

**Remote models:**
- "Download" — MudSelect for variant (filename, size, quantization), then download button
- Download progress bar (inline, appears during download)
- Download root auto-resolved from Settings (provider + type)
- "View on CivitAI/HuggingFace" — external link

### Details Section (scrollable)
- Full description (HTML rendered for CivitAI, plain text otherwise)
- Metadata table: Base Model, File Size, Format, Tags
- Remote-specific: Download Count, Rating, Trigger Words (CivitAI)
- Local-specific: File Path, Detected Date, Last Verified, Status

## Download Storage Settings

### New Section in Settings > Storage Roots

"Download Locations" section below existing storage root list:

Per-provider card showing:
- Provider name and icon
- Download root path text field (e.g., `G:\models\civitai\`)
- Preview of auto-created subfolders: Checkpoints/, LoRA/, VAE/, Embeddings/, ControlNet/
- Save button

### DownloadLocationSettings Value Object

```csharp
public sealed record DownloadLocationSettings
{
    public Dictionary<string, string> ProviderRoots { get; init; } = new();

    public string GetDownloadPath(string providerId, ModelType type, string defaultRoot)
    {
        var root = ProviderRoots.GetValueOrDefault(providerId,
            Path.Combine(defaultRoot, "Downloads", providerId));
        var typeFolder = type switch
        {
            ModelType.Checkpoint => "Checkpoints",
            ModelType.LoRA => "LoRA",
            ModelType.VAE => "VAE",
            ModelType.Embedding => "Embeddings",
            ModelType.ControlNet => "ControlNet",
            _ => "Other"
        };
        return Path.Combine(root, typeFolder);
    }

    public static DownloadLocationSettings Default => new();
}
```

Persisted via `ISettingsProvider.SetAsync<DownloadLocationSettings>("DownloadLocations", ...)`.

### Download Flow

1. User clicks "Download" in drawer, selects variant
2. Resolve download directory: `DownloadLocationSettings.GetDownloadPath(providerId, modelType, appPaths.AssetsDirectory)`
3. Create directory if needed
4. Download file to `{directory}/{filename}`
5. After completion: auto-register the provider's download root as a Storage Root (if not already), trigger a scan of that root so the model appears in the local catalog immediately

## Provider Improvements

### CivitAI Provider

**Fixes:**
- Add `{ ModelType.ControlNet, "Controlnet" }` to `CivitTypeMap`
- Map `ModelSearchQuery.Sort` to API sort param: Relevance→"Most Downloaded", MostDownloaded→"Most Downloaded", Newest→"Newest", HighestRated→"Highest Rated"
- Pass `baseModels` query param from `ModelSearchQuery.Family`: SD15→"SD 1.5", SDXL→"SDXL 1.0", Flux→"Flux.1 D,Flux.1 S", Pony→"Pony"
- Pass `nsfw=false` when content safety filter mode is not Off
- Extract `trainedWords` from `modelVersions[0]` and include in `RemoteModelInfo.Tags`
- Expose all file variants from `modelVersions[0].files[]` (not just first)

**Enhancements:**
- Set `ProviderUrl` to `https://civitai.com/models/{modelId}`
- Include model version name in the title if multiple versions exist

### HuggingFace Provider

**Overhaul:**
- After search results, fetch `/api/models/{id}` for each result to get richer metadata (card data, tags, siblings/files)
- Or better: use the `?expand[]=siblings` query param on the search endpoint to get file listing inline
- Detect model type from pipeline tags: `text-to-image`→Checkpoint, check for `lora`/`embedding` in tags
- Detect model family from tags: look for `stable-diffusion`, `sdxl`, `flux` in the tag list
- Preview image: construct from `https://huggingface.co/{id}/resolve/main/images/preview.png` or use card thumbnail
- File variants: list `.safetensors` and `.gguf` files from siblings, with sizes
- Set `ProviderUrl` to `https://huggingface.co/{id}`
- Fix download URL to use actual filenames from file listing

## File Structure

### Domain
- Create: `ValueObjects/DownloadLocationSettings.cs`

### Application
- Create: `DTOs/ModelCardViewModel.cs`
- No changes to existing DTOs or interfaces (ModelSearchQuery already has Family/Sort/Tag)

### Infrastructure
- Modify: `ModelSources/CivitAIProvider.cs`
- Modify: `ModelSources/HuggingFaceProvider.cs`

### Web
- Rewrite: `Pages/Models.razor`
- Create: `Shared/ModelCardUnified.razor`
- Create: `Shared/ModelListRow.razor`
- Create: `Shared/ModelDetailDrawer.razor`
- Create: `Shared/ModelFilterBar.razor`
- Modify: `Pages/Settings.razor` (add Download Locations section)

### Tests
- Domain tests for DownloadLocationSettings
- Provider tests for new CivitAI/HuggingFace mapping logic
