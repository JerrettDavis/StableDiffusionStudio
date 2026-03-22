# Data Model: Models Page Revamp

## New Value Objects

### DownloadLocationSettings

**Layer**: Domain (ValueObjects)
**Type**: Immutable sealed record
**Storage**: Serialized to JSON via existing `ISettingsProvider` (key: `"DownloadLocations"`)

| Field | Type | Description |
|-------|------|-------------|
| ProviderRoots | `Dictionary<string, string>` | Maps provider ID → download root path |

**Methods**:
- `GetDownloadPath(providerId, modelType, defaultRoot)` → resolves full path with type subfolder
- `static Default` → empty dictionary (uses app default paths)

**Type Subfolder Mapping**:
| ModelType | Subfolder |
|-----------|-----------|
| Checkpoint | `Checkpoints/` |
| LoRA | `LoRA/` |
| VAE | `VAE/` |
| Embedding | `Embeddings/` |
| ControlNet | `ControlNet/` |
| _ (other) | `Other/` |

## New DTOs

### ModelCardViewModel

**Layer**: Application (DTOs)
**Type**: Positional record
**Purpose**: Normalize both `ModelRecordDto` (local) and `RemoteModelInfo` (remote) into a single display model

| Field | Type | Source (Local) | Source (Remote) |
|-------|------|----------------|-----------------|
| Id | string | `Guid.ToString()` | `ExternalId` |
| Title | string | `Title` | `Title` |
| PreviewImageUrl | string? | Local preview path or null | `PreviewImageUrl` |
| Type | ModelType | `Type` | `Type` |
| Family | ModelFamily | `ModelFamily` | `Family` |
| Format | ModelFormat | `Format` | `Format` |
| FileSize | long? | `FileSize` | `FileSize` |
| Source | string | `"local"` | `ProviderId` |
| IsLocal | bool | `true` | `false` |
| IsAvailable | bool | `Status == Available` | `true` |
| Description | string? | `Description` | `Description` |

**Static factory methods**:
- `FromLocal(ModelRecordDto dto, IAppPaths appPaths)` → creates from local model
- `FromRemote(RemoteModelInfo info, string providerId)` → creates from remote model

## Existing Entities (unchanged)

### ModelRecord (Domain Entity)
No changes. Existing fields are sufficient.

### RemoteModelInfo (Application DTO)
No structural changes. Fields populated differently by improved providers:
- `PreviewImageUrl`: Now populated by HuggingFace (was always null)
- `Variants`: Now populated by HuggingFace (was always empty)
- `Type`: Now detected from HF tags (was hardcoded Checkpoint)
- `Family`: Now detected from HF tags (was hardcoded Unknown)

### ModelSearchQuery (Application DTO)
No structural changes. Existing `Family`, `Sort`, `Tag` fields are now wired to the UI filter bar and passed to provider APIs.

## No Database Changes

This feature adds no new database tables. All persistence uses:
- `ISettingsProvider` for `DownloadLocationSettings` (key-value store in existing Settings table)
- Existing `ModelRecords` table for local catalog (no schema changes)
