# Unified Model Provider System Design

**Date:** 2026-03-19
**Status:** Draft
**Approach:** Capability-driven unified interface, vertical slice implementation

---

## 1. Overview

Replace the current `IModelSourceAdapter` with a unified `IModelProvider` interface that supports local scanning, remote searching, downloading, and authentication through capability-driven dispatch. Add model type classification (Checkpoint, VAE, LoRA, Embedding, ControlNet, Upscaler) to the domain model. Implement HuggingFace and CivitAI as the first remote providers.

### Success Criteria

1. Local folder scanning still works (backward compatible)
2. Users can search HuggingFace and CivitAI from within the app
3. Users can download models from remote providers to local storage
4. Download progress is visible in the Jobs UI
5. Models are classified by type (Checkpoint, VAE, LoRA, etc.)
6. Provider credentials can be configured in Settings
7. All providers are registered via DI — adding a new provider requires only implementing `IModelProvider` and registering it

---

## 2. Domain Changes

### New Enum: ModelType

```csharp
public enum ModelType
{
    Unknown,
    Checkpoint,
    VAE,
    LoRA,
    Embedding,
    ControlNet,
    Upscaler
}
```

### ModelRecord Changes

Add `ModelType` property to `ModelRecord`:
- `ModelType Type { get; private set; }` — defaults to `Checkpoint` for existing records
- `Create()` factory method gets a `ModelType type = ModelType.Checkpoint` parameter (default preserves backward compat)
- `UpdateMetadata()` gets an optional `ModelType? type` parameter
- EF migration adds `Type` column with default value `Checkpoint`

### ModelFileAnalyzer Changes

Add `InferModelType(ModelFileInfo info)` method with explicit precedence order (filename checks first, then size):

1. **Filename-based (highest priority):**
   - Contains "controlnet" or "control_" → ControlNet
   - Contains "upscale", "esrgan", "swinir" → Upscaler
   - Contains "vae" → VAE
   - Contains "lora" or in a `Lora/` directory → LoRA
   - Contains "embedding" or "ti-" → Embedding
2. **Size-based (fallback):**
   - safetensors/pt/bin < 50MB → Embedding
   - safetensors < 200MB → LoRA
   - safetensors 300-800MB → VAE
   - safetensors/ckpt > 1GB → Checkpoint
3. **Default:** Unknown

---

## 3. Application Layer

### IModelProvider Interface

```csharp
public interface IModelProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    ModelProviderCapabilities Capabilities { get; }

    // Local scanning — only called when CanScanLocal
    Task<IReadOnlyList<DiscoveredModel>> ScanLocalAsync(
        StorageRoot root, CancellationToken ct = default);

    // Remote search — only called when CanSearch
    Task<SearchResult> SearchAsync(
        ModelSearchQuery query, CancellationToken ct = default);

    // Download — only called when CanDownload
    Task<DownloadResult> DownloadAsync(
        DownloadRequest request, IProgress<DownloadProgress> progress,
        CancellationToken ct = default);

    // Auth validation — only called when RequiresAuth
    Task<bool> ValidateCredentialsAsync(CancellationToken ct = default);
}
```

### Capabilities

```csharp
public record ModelProviderCapabilities(
    bool CanScanLocal,
    bool CanSearch,
    bool CanDownload,
    bool RequiresAuth,
    IReadOnlyList<ModelType> SupportedModelTypes);
```

### Search Types

```csharp
public record ModelSearchQuery(
    string ProviderId,
    string? SearchTerm = null,
    ModelType? Type = null,
    ModelFamily? Family = null,
    string? Tag = null,
    SortOrder Sort = SortOrder.Relevance,
    int Page = 0,
    int PageSize = 20);

public enum SortOrder { Relevance, Newest, MostDownloaded, Name }

public record SearchResult(
    IReadOnlyList<RemoteModelInfo> Models,
    int TotalCount,
    bool HasMore);

public record RemoteModelInfo(
    string ExternalId,
    string Title,
    string? Description,
    ModelType Type,
    ModelFamily Family,
    ModelFormat Format,
    long? FileSize,
    string? PreviewImageUrl,
    IReadOnlyList<string> Tags,
    string ProviderUrl,
    IReadOnlyList<ModelFileVariant> Variants);

public record ModelFileVariant(
    string FileName,
    long FileSize,
    ModelFormat Format,
    string? Quantization);
```

### Download Types

```csharp
public record DownloadRequest(
    string ProviderId,
    string ExternalId,
    string? VariantFileName,
    StorageRoot TargetRoot,
    ModelType Type);

public record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    string Phase);

public record DownloadResult(
    bool Success,
    string? LocalFilePath,
    string? Error);
```

### IProviderCredentialStore

```csharp
public interface IProviderCredentialStore
{
    Task<string?> GetTokenAsync(string providerId, CancellationToken ct = default);
    Task SetTokenAsync(string providerId, string token, CancellationToken ct = default);
    Task RemoveTokenAsync(string providerId, CancellationToken ct = default);
}
```

### ModelCatalogService Changes

- Constructor takes `IEnumerable<IModelProvider>` instead of `IEnumerable<IModelSourceAdapter>`
- `ScanAsync` iterates providers where `CanScanLocal == true`
- New `SearchAsync(ModelSearchQuery)` method dispatches to the provider matching `query.ProviderId`
- New `DownloadAsync(DownloadRequest)` enqueues a download job
- `ScanLocalAsync` returns `DiscoveredModel` which is mapped to `ModelRecord` with `ModelType`

### DiscoveredModel (replaces raw ModelRecord from scan)

```csharp
public record DiscoveredModel(
    string FilePath,
    string? Title,
    ModelType Type,
    ModelFamily Family,
    ModelFormat Format,
    long FileSize,
    string? PreviewImagePath,
    string? Description,
    IReadOnlyList<string> Tags);
```

This separates the scan result from the persisted entity — providers return `DiscoveredModel`, the service maps it to `ModelRecord`.

---

## 4. Infrastructure: Provider Implementations

### LocalFolderProvider

Refactor `LocalFolderAdapter` → `LocalFolderProvider : IModelProvider`:
- `ProviderId = "local-folder"`, `DisplayName = "Local Folder"`
- `Capabilities = (CanScanLocal: true, CanSearch: false, CanDownload: false, RequiresAuth: false, SupportedModelTypes: all)`
- `ScanLocalAsync` — existing scan logic, now also infers `ModelType` via `ModelFileAnalyzer`
- `SearchAsync` — returns empty `SearchResult([], 0, false)`
- `DownloadAsync` — returns `DownloadResult(false, null, "Local provider does not support downloads")`
- `ValidateCredentialsAsync` — returns `true` (no auth needed)
- Non-applicable methods return safe defaults rather than throwing exceptions

### HuggingFaceProvider

New class `HuggingFaceProvider : IModelProvider`:
- `ProviderId = "huggingface"`, `DisplayName = "Hugging Face"`
- `Capabilities = (CanScanLocal: false, CanSearch: true, CanDownload: true, RequiresAuth: false (optional for gated models), SupportedModelTypes: [Checkpoint, VAE, LoRA, Embedding, ControlNet])`

**Search:** Uses HF Hub API `GET https://huggingface.co/api/models`:
- Query params: `search`, `filter` (by pipeline_tag, library), `sort` (downloads, likes, lastModified), `limit`, `offset`
- Maps `pipeline_tag: "text-to-image"` → Checkpoint, filters by `diffusers`, `safetensors` libraries
- Extracts model card metadata for description, tags
- Lists files from `GET /api/models/{id}/tree/main` to find safetensors/ckpt variants

**Download:** HTTP GET to `https://huggingface.co/resolve/{model_id}/main/{filename}`:
- Supports range requests for resume
- Auth via Bearer token in header for gated models
- Streams to target directory with progress reporting

**Auth:** Bearer token from `IProviderCredentialStore`. Optional — public models work without auth.

### CivitAIProvider

New class `CivitAIProvider : IModelProvider`:
- `ProviderId = "civitai"`, `DisplayName = "CivitAI"`
- `Capabilities = (CanScanLocal: false, CanSearch: true, CanDownload: true, RequiresAuth: false (optional for NSFW/early access), SupportedModelTypes: [Checkpoint, VAE, LoRA, Embedding, ControlNet])`

**Search:** Uses CivitAI REST API `GET https://civitai.com/api/v1/models`:
- Query params: `query`, `types` (Checkpoint, LORA, VAE, etc.), `sort` (Highest Rated, Most Downloaded, Newest), `limit`, `page`
- Maps CivitAI model types to our `ModelType` enum
- Extracts version info, file variants, preview images

**Download:** HTTP GET to the download URL from the model version:
- `https://civitai.com/api/download/models/{versionId}`
- Auth via API key query param or header
- Streams with progress

**Auth:** API key from `IProviderCredentialStore`. Optional for most models.

### ProviderCredentialStore

Implements `IProviderCredentialStore` using `ISettingsProvider`:
- Keys stored as `provider-credentials:{providerId}`
- Values are the token/API key strings
- In future, can be upgraded to OS keychain (DPAPI/Keychain/libsecret)

---

## 5. Download Infrastructure

### DownloadJob Integration

Uses existing background job system:
- New job type `"model-download"`
- `ModelDownloadJobHandler : IJobHandler` — resolves provider, calls `DownloadAsync`, reports progress, registers model in catalog on completion
- Job `Data` field stores serialized `DownloadRequest`
- Progress pushed to UI via `JobRecord.UpdateProgress()` — Jobs page polls via timer (existing pattern), SignalR push is future enhancement

### HttpDownloadClient

Shared HTTP download utility used by HuggingFace and CivitAI providers:
- Chunked streaming download
- Progress reporting via `IProgress<DownloadProgress>`
- Range header support for resume (best-effort)
- Configurable timeout and retry
- File integrity check (size match)

---

## 6. Presentation Changes

### Models Page Updates

- Source tab/filter: "All", "Local", "Hugging Face", "CivitAI"
- When a remote provider is selected, search queries go to that provider's API
- Remote results show a "Download" button instead of local file info
- Download button enqueues a download job and shows snackbar confirmation
- Model cards show provider badge/icon

### Settings Page Updates

- New "Model Sources" expansion panel:
  - List of registered providers with status (connected/not configured)
  - HuggingFace: token input field, validate button
  - CivitAI: API key input field, validate button

### ModelFilter Changes

Add `ModelType? Type` filter to support filtering by Checkpoint/VAE/LoRA etc. in both local catalog and remote search.

---

## 7. Migration Strategy

1. Add `ModelType` enum and property to domain
2. EF migration: add `Type` column with default `Checkpoint`
3. Rename `IModelSourceAdapter` → `IModelProvider` with new methods
4. Refactor `LocalFolderAdapter` → `LocalFolderProvider`
5. Update `ModelCatalogService` to use `IModelProvider`
6. Update all DI registrations
7. Update existing tests
8. Add HuggingFace provider
9. Add CivitAI provider
10. Add download infrastructure
11. Update UI

---

## 8. Testing Strategy

### Domain Tests
- `ModelFileAnalyzer.InferModelType` — size/name heuristics for all model types
- `ModelRecord` — new `ModelType` property behavior

### Application Tests
- `ModelCatalogService.SearchAsync` — dispatches to correct provider
- `ModelCatalogService.DownloadAsync` — enqueues job correctly

### Infrastructure Tests
- `LocalFolderProvider` — backward-compatible scan with model type inference
- `HuggingFaceProvider` — mock HTTP responses, verify search/download behavior
- `CivitAIProvider` — mock HTTP responses, verify search/download behavior
- `HttpDownloadClient` — chunked download, progress reporting, resume
- `ProviderCredentialStore` — store/retrieve/remove credentials

### E2E Tests
- Search HuggingFace from Models page (mocked API in test)
- Configure provider credentials in Settings

---

## 9. New Files Summary

### Domain
- `src/StableDiffusionStudio.Domain/Enums/ModelType.cs`

### Application
- `src/StableDiffusionStudio.Application/Interfaces/IModelProvider.cs`
- `src/StableDiffusionStudio.Application/Interfaces/IProviderCredentialStore.cs`
- `src/StableDiffusionStudio.Application/DTOs/ModelProviderCapabilities.cs`
- `src/StableDiffusionStudio.Application/DTOs/DiscoveredModel.cs`
- `src/StableDiffusionStudio.Application/DTOs/ModelSearchQuery.cs`
- `src/StableDiffusionStudio.Application/DTOs/SearchResult.cs`
- `src/StableDiffusionStudio.Application/DTOs/RemoteModelInfo.cs`
- `src/StableDiffusionStudio.Application/DTOs/ModelFileVariant.cs`
- `src/StableDiffusionStudio.Application/DTOs/DownloadRequest.cs`
- `src/StableDiffusionStudio.Application/DTOs/DownloadProgress.cs`
- `src/StableDiffusionStudio.Application/DTOs/DownloadResult.cs`
- `src/StableDiffusionStudio.Application/DTOs/SortOrder.cs`

### Infrastructure
- `src/StableDiffusionStudio.Infrastructure/ModelSources/LocalFolderProvider.cs` (rename from LocalFolderAdapter)
- `src/StableDiffusionStudio.Infrastructure/ModelSources/HuggingFaceProvider.cs`
- `src/StableDiffusionStudio.Infrastructure/ModelSources/CivitAIProvider.cs`
- `src/StableDiffusionStudio.Infrastructure/ModelSources/HttpDownloadClient.cs`
- `src/StableDiffusionStudio.Infrastructure/Settings/ProviderCredentialStore.cs`
- `src/StableDiffusionStudio.Infrastructure/Jobs/ModelDownloadJobHandler.cs`

### Modified
- `src/StableDiffusionStudio.Domain/Entities/ModelRecord.cs` — add ModelType
- `src/StableDiffusionStudio.Domain/Services/ModelFileAnalyzer.cs` — add InferModelType
- `src/StableDiffusionStudio.Application/Services/ModelCatalogService.cs` — use IModelProvider, add Search/Download
- `src/StableDiffusionStudio.Application/DTOs/ModelFilter.cs` — add ModelType filter
- `src/StableDiffusionStudio.Application/DTOs/ModelRecordDto.cs` — add ModelType
- `src/StableDiffusionStudio.Web/Components/Pages/Models.razor` — source tabs, download button
- `src/StableDiffusionStudio.Web/Components/Pages/Settings.razor` — provider credentials
- `src/StableDiffusionStudio.Web/Program.cs` — register new providers

---

## 10. Scope Notes

- **Forge Local Adapter** — deferred to a follow-up. Not in scope for this sub-project.
- **Pause/Cancel downloads** — CancellationToken cancellation stops the download. True pause-resume (persisting byte offset across app restarts) is deferred.
- **Credential security** — Initial implementation stores tokens via `ISettingsProvider` (SQLite). Upgrade to OS keychain (DPAPI/Keychain) is accepted tech debt for v1.
- **IJobHandler location** — remains in Infrastructure. It's an implementation concern, not an application contract. Providers depend on Application interfaces; job handlers depend on both Application and Infrastructure — correct directionally.

---

## 11. Files Summary

### Deleted
- `src/StableDiffusionStudio.Application/Interfaces/IModelSourceAdapter.cs` (replaced by IModelProvider)
- `src/StableDiffusionStudio.Application/DTOs/ModelSourceCapabilities.cs` (replaced by ModelProviderCapabilities)
- `src/StableDiffusionStudio.Infrastructure/ModelSources/LocalFolderAdapter.cs` (renamed to LocalFolderProvider)
