# Unified Model Provider System Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace IModelSourceAdapter with a unified IModelProvider supporting local scanning, remote search (HuggingFace, CivitAI), downloads, and model type classification.

**Architecture:** Capability-driven unified interface. Providers declare what they support (scan, search, download, auth). ModelCatalogService dispatches to the right provider based on capabilities. Background job system handles downloads.

**Tech Stack:** .NET 10, EF Core, HttpClient, HuggingFace Hub API, CivitAI REST API, existing job system

**Spec:** `docs/superpowers/specs/2026-03-19-unified-model-provider-design.md`

---

## Chunk 1: Domain & Application Layer (Tasks 1-4)

### Task 1: Add ModelType Enum and Update ModelRecord

**Files:**
- Create: `src/StableDiffusionStudio.Domain/Enums/ModelType.cs`
- Modify: `src/StableDiffusionStudio.Domain/Entities/ModelRecord.cs`
- Modify: `src/StableDiffusionStudio.Domain/Services/ModelFileAnalyzer.cs`
- Modify: `src/StableDiffusionStudio.Infrastructure/Persistence/Configurations/ModelRecordConfiguration.cs`
- Create: `tests/StableDiffusionStudio.Domain.Tests/Services/ModelFileAnalyzerModelTypeTests.cs`
- Modify: `tests/StableDiffusionStudio.Domain.Tests/Entities/ModelRecordTests.cs`

- [ ] **Step 1: Create ModelType enum**

Create `src/StableDiffusionStudio.Domain/Enums/ModelType.cs`:
```csharp
namespace StableDiffusionStudio.Domain.Enums;

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

- [ ] **Step 2: Write failing tests for ModelFileAnalyzer.InferModelType**

Create `tests/StableDiffusionStudio.Domain.Tests/Services/ModelFileAnalyzerModelTypeTests.cs`:
```csharp
using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class ModelFileAnalyzerModelTypeTests
{
    [Theory]
    [InlineData("controlnet-canny.safetensors", 1_500_000_000L, ModelType.ControlNet)]
    [InlineData("control_v11p_sd15.safetensors", 1_500_000_000L, ModelType.ControlNet)]
    [InlineData("RealESRGAN_x4plus.pth", 67_000_000L, ModelType.Upscaler)]
    [InlineData("4x-UltraSharp.safetensors", 67_000_000L, ModelType.Upscaler)]
    [InlineData("sd-vae-ft-mse.safetensors", 335_000_000L, ModelType.VAE)]
    [InlineData("kl-f8-anime2.vae.safetensors", 335_000_000L, ModelType.VAE)]
    [InlineData("my-lora-v1.safetensors", 150_000_000L, ModelType.LoRA)]
    [InlineData("Models/Lora/detail-tweaker.safetensors", 50_000_000L, ModelType.LoRA)]
    [InlineData("embedding-easyneg.safetensors", 30_000_000L, ModelType.Embedding)]
    [InlineData("ti-badhands.pt", 20_000_000L, ModelType.Embedding)]
    public void InferModelType_FromFilename_ReturnsCorrectType(string fileName, long fileSize, ModelType expected)
    {
        var info = new ModelFileInfo(fileName, fileSize, null);
        ModelFileAnalyzer.InferModelType(info).Should().Be(expected);
    }

    [Theory]
    [InlineData("unknown-model.safetensors", 30_000_000L, ModelType.Embedding)]
    [InlineData("unknown-model.safetensors", 150_000_000L, ModelType.LoRA)]
    [InlineData("unknown-model.safetensors", 500_000_000L, ModelType.VAE)]
    [InlineData("unknown-model.safetensors", 2_000_000_000L, ModelType.Checkpoint)]
    [InlineData("unknown-model.safetensors", 5_000L, ModelType.Unknown)]
    public void InferModelType_FromSize_ReturnsCorrectType(string fileName, long fileSize, ModelType expected)
    {
        var info = new ModelFileInfo(fileName, fileSize, null);
        ModelFileAnalyzer.InferModelType(info).Should().Be(expected);
    }
}
```

- [ ] **Step 3: Implement InferModelType in ModelFileAnalyzer**

Add to `src/StableDiffusionStudio.Domain/Services/ModelFileAnalyzer.cs`:
```csharp
public static ModelType InferModelType(ModelFileInfo info)
{
    var name = info.FileName.ToLowerInvariant();

    // Filename-based (highest priority)
    if (name.Contains("controlnet") || name.Contains("control_"))
        return ModelType.ControlNet;
    if (name.Contains("upscale") || name.Contains("esrgan") || name.Contains("swinir") || name.Contains("ultrasharp"))
        return ModelType.Upscaler;
    if (name.Contains("vae") || name.Contains(".vae."))
        return ModelType.VAE;
    if (name.Contains("lora") || info.FileName.Contains("Lora/") || info.FileName.Contains("Lora\\"))
        return ModelType.LoRA;
    if (name.Contains("embedding") || name.Contains("ti-"))
        return ModelType.Embedding;

    // Size-based fallback
    var sizeMb = info.FileSize / 1_000_000.0;
    var ext = Path.GetExtension(info.FileName).ToLowerInvariant();

    return sizeMb switch
    {
        < 50 when ext is ".safetensors" or ".pt" or ".bin" => ModelType.Embedding,
        < 200 when ext is ".safetensors" => ModelType.LoRA,
        >= 300 and <= 800 when ext is ".safetensors" => ModelType.VAE,
        > 1000 => ModelType.Checkpoint,
        _ => ModelType.Unknown
    };
}
```

- [ ] **Step 4: Add ModelType to ModelRecord**

Modify `src/StableDiffusionStudio.Domain/Entities/ModelRecord.cs`:
- Add property: `public ModelType Type { get; private set; }`
- Update `Create()`: add `ModelType type = ModelType.Checkpoint` parameter, set `Type = type`
- Update `UpdateMetadata()`: add `ModelType? type = null` parameter

- [ ] **Step 5: Add ModelType test for ModelRecord**

Add to `tests/StableDiffusionStudio.Domain.Tests/Entities/ModelRecordTests.cs`:
```csharp
[Fact]
public void Create_WithModelType_SetsType()
{
    var record = ModelRecord.Create(null, "/path/lora.safetensors",
        ModelFamily.SD15, ModelFormat.SafeTensors, 150_000_000L, "local", ModelType.LoRA);
    record.Type.Should().Be(ModelType.LoRA);
}

[Fact]
public void Create_DefaultType_IsCheckpoint()
{
    var record = ModelRecord.Create(null, "/path/model.safetensors",
        ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "local");
    record.Type.Should().Be(ModelType.Checkpoint);
}
```

- [ ] **Step 6: Update EF configuration for ModelType**

Add to `ModelRecordConfiguration.cs`:
```csharp
builder.Property(m => m.Type).HasConversion<string>().HasDefaultValue(ModelType.Checkpoint);
builder.HasIndex(m => m.Type);
```

- [ ] **Step 7: Run all tests, verify pass**

Run: `dotnet test --filter "FullyQualifiedName!~E2E"`
Expected: All tests pass including new ModelType tests.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: add ModelType enum and model type inference

ModelType enum (Unknown, Checkpoint, VAE, LoRA, Embedding, ControlNet,
Upscaler). ModelFileAnalyzer.InferModelType with filename-first,
size-fallback heuristics. ModelRecord.Type property with EF config."
```

---

### Task 2: Define IModelProvider Interface and Supporting Types

**Files:**
- Create: `src/StableDiffusionStudio.Application/Interfaces/IModelProvider.cs`
- Create: `src/StableDiffusionStudio.Application/Interfaces/IProviderCredentialStore.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ModelProviderCapabilities.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/DiscoveredModel.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ModelSearchQuery.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/RemoteModelInfo.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/ModelFileVariant.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/SearchResult.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/DownloadRequest.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/DownloadProgress.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/DownloadResult.cs`
- Create: `src/StableDiffusionStudio.Application/DTOs/SortOrder.cs`
- Delete: `src/StableDiffusionStudio.Application/Interfaces/IModelSourceAdapter.cs`
- Delete: `src/StableDiffusionStudio.Application/DTOs/ModelSourceCapabilities.cs`

- [ ] **Step 1: Create all new DTOs**

Create each as a separate file. All are simple records:

`SortOrder.cs`:
```csharp
namespace StableDiffusionStudio.Application.DTOs;
public enum SortOrder { Relevance, Newest, MostDownloaded, Name }
```

`ModelProviderCapabilities.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;
namespace StableDiffusionStudio.Application.DTOs;
public record ModelProviderCapabilities(
    bool CanScanLocal, bool CanSearch, bool CanDownload,
    bool RequiresAuth, IReadOnlyList<ModelType> SupportedModelTypes);
```

`DiscoveredModel.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;
namespace StableDiffusionStudio.Application.DTOs;
public record DiscoveredModel(
    string FilePath, string? Title, ModelType Type, ModelFamily Family,
    ModelFormat Format, long FileSize, string? PreviewImagePath,
    string? Description, IReadOnlyList<string> Tags);
```

`ModelSearchQuery.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;
namespace StableDiffusionStudio.Application.DTOs;
public record ModelSearchQuery(
    string ProviderId, string? SearchTerm = null, ModelType? Type = null,
    ModelFamily? Family = null, string? Tag = null,
    SortOrder Sort = SortOrder.Relevance, int Page = 0, int PageSize = 20);
```

`RemoteModelInfo.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;
namespace StableDiffusionStudio.Application.DTOs;
public record RemoteModelInfo(
    string ExternalId, string Title, string? Description,
    ModelType Type, ModelFamily Family, ModelFormat Format,
    long? FileSize, string? PreviewImageUrl, IReadOnlyList<string> Tags,
    string ProviderUrl, IReadOnlyList<ModelFileVariant> Variants);
```

`ModelFileVariant.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;
namespace StableDiffusionStudio.Application.DTOs;
public record ModelFileVariant(string FileName, long FileSize, ModelFormat Format, string? Quantization);
```

`SearchResult.cs`:
```csharp
namespace StableDiffusionStudio.Application.DTOs;
public record SearchResult(IReadOnlyList<RemoteModelInfo> Models, int TotalCount, bool HasMore);
```

`DownloadRequest.cs`:
```csharp
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
namespace StableDiffusionStudio.Application.DTOs;
public record DownloadRequest(
    string ProviderId, string ExternalId, string? VariantFileName,
    StorageRoot TargetRoot, ModelType Type);
```

`DownloadProgress.cs`:
```csharp
namespace StableDiffusionStudio.Application.DTOs;
public record DownloadProgress(long BytesDownloaded, long TotalBytes, string Phase);
```

`DownloadResult.cs`:
```csharp
namespace StableDiffusionStudio.Application.DTOs;
public record DownloadResult(bool Success, string? LocalFilePath, string? Error);
```

- [ ] **Step 2: Create IModelProvider interface**

Create `src/StableDiffusionStudio.Application/Interfaces/IModelProvider.cs`:
```csharp
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IModelProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    ModelProviderCapabilities Capabilities { get; }

    Task<IReadOnlyList<DiscoveredModel>> ScanLocalAsync(
        StorageRoot root, CancellationToken ct = default);

    Task<SearchResult> SearchAsync(
        ModelSearchQuery query, CancellationToken ct = default);

    Task<DownloadResult> DownloadAsync(
        DownloadRequest request, IProgress<DownloadProgress> progress,
        CancellationToken ct = default);

    Task<bool> ValidateCredentialsAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Create IProviderCredentialStore interface**

Create `src/StableDiffusionStudio.Application/Interfaces/IProviderCredentialStore.cs`:
```csharp
namespace StableDiffusionStudio.Application.Interfaces;

public interface IProviderCredentialStore
{
    Task<string?> GetTokenAsync(string providerId, CancellationToken ct = default);
    Task SetTokenAsync(string providerId, string token, CancellationToken ct = default);
    Task RemoveTokenAsync(string providerId, CancellationToken ct = default);
}
```

- [ ] **Step 4: Delete old interface and DTO**

Delete `src/StableDiffusionStudio.Application/Interfaces/IModelSourceAdapter.cs`
Delete `src/StableDiffusionStudio.Application/DTOs/ModelSourceCapabilities.cs`

- [ ] **Step 5: Update ModelFilter and ModelRecordDto**

Add `ModelType? Type` to `ModelFilter`:
```csharp
public record ModelFilter(
    string? SearchTerm = null, ModelFamily? Family = null, ModelFormat? Format = null,
    ModelStatus? Status = null, string? Source = null, ModelType? Type = null,
    int Skip = 0, int Take = 50);
```

Add `ModelType Type` to `ModelRecordDto`:
```csharp
public record ModelRecordDto(
    Guid Id, string Title, ModelType Type, ModelFamily ModelFamily, ModelFormat Format,
    string FilePath, long FileSize, string Source, IReadOnlyList<string> Tags,
    string? Description, string? PreviewImagePath, string? CompatibilityHints,
    ModelStatus Status, DateTimeOffset DetectedAt);
```

- [ ] **Step 6: Verify build compiles (will have errors in Infrastructure — expected)**

Run: `dotnet build src/StableDiffusionStudio.Application`
Expected: Builds successfully. Infrastructure/Web will fail until updated in later tasks.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: define IModelProvider interface and supporting types

Unified IModelProvider with ScanLocal, Search, Download, ValidateCredentials.
ModelProviderCapabilities, DiscoveredModel, RemoteModelInfo, SearchResult,
DownloadRequest/Progress/Result DTOs. IProviderCredentialStore for auth.
Removes IModelSourceAdapter and ModelSourceCapabilities."
```

---

### Task 3: Update ModelCatalogService for IModelProvider

**Files:**
- Modify: `src/StableDiffusionStudio.Application/Services/ModelCatalogService.cs`
- Modify: `tests/StableDiffusionStudio.Application.Tests/Services/ModelCatalogServiceTests.cs`

- [ ] **Step 1: Update ModelCatalogService**

Replace `IEnumerable<IModelSourceAdapter>` with `IEnumerable<IModelProvider>`. Update `ScanAsync` to use `ScanLocalAsync` and map `DiscoveredModel` to `ModelRecord`. Add `SearchAsync` and `RequestDownloadAsync` methods:

```csharp
public class ModelCatalogService
{
    private readonly IModelCatalogRepository _repository;
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly IStorageRootProvider _rootProvider;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<ModelCatalogService>? _logger;

    public ModelCatalogService(
        IModelCatalogRepository repository,
        IEnumerable<IModelProvider> providers,
        IStorageRootProvider rootProvider,
        IJobQueue jobQueue,
        ILogger<ModelCatalogService>? logger = null)
    { ... }

    public async Task<ScanResult> ScanAsync(ScanModelsCommand command, CancellationToken ct = default)
    {
        // Iterate providers where CanScanLocal, scan roots, map DiscoveredModel → ModelRecord
    }

    public async Task<SearchResult> SearchAsync(ModelSearchQuery query, CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderId == query.ProviderId);
        if (provider is null || !provider.Capabilities.CanSearch)
            return new SearchResult([], 0, false);
        return await provider.SearchAsync(query, ct);
    }

    public async Task<Guid> RequestDownloadAsync(DownloadRequest request, CancellationToken ct = default)
    {
        return await _jobQueue.EnqueueAsync("model-download",
            System.Text.Json.JsonSerializer.Serialize(request), ct);
    }

    public IReadOnlyList<ModelProviderInfo> GetProviders()
    {
        return _providers.Select(p => new ModelProviderInfo(
            p.ProviderId, p.DisplayName, p.Capabilities)).ToList();
    }
}

public record ModelProviderInfo(string ProviderId, string DisplayName, ModelProviderCapabilities Capabilities);
```

- [ ] **Step 2: Update tests**

Update `ModelCatalogServiceTests` to use `IModelProvider` instead of `IModelSourceAdapter`. Add tests for `SearchAsync` and `RequestDownloadAsync`:

```csharp
[Fact]
public async Task SearchAsync_DispatchesToCorrectProvider()
{
    var query = new ModelSearchQuery("test-provider", SearchTerm: "stable diffusion");
    _provider.Capabilities.Returns(new ModelProviderCapabilities(false, true, true, false, new[] { ModelType.Checkpoint }));
    _provider.ProviderId.Returns("test-provider");
    _provider.SearchAsync(query).Returns(new SearchResult(new[] { /* mock result */ }, 1, false));

    var result = await _service.SearchAsync(query);
    result.TotalCount.Should().Be(1);
}

[Fact]
public async Task SearchAsync_UnknownProvider_ReturnsEmpty()
{
    var query = new ModelSearchQuery("nonexistent");
    var result = await _service.SearchAsync(query);
    result.TotalCount.Should().Be(0);
}

[Fact]
public async Task RequestDownloadAsync_EnqueuesJob()
{
    var request = new DownloadRequest("hf", "model-id", null,
        new StorageRoot("/models", "Models"), ModelType.Checkpoint);
    await _service.RequestDownloadAsync(request);
    await _jobQueue.Received(1).EnqueueAsync("model-download", Arg.Any<string>(), Arg.Any<CancellationToken>());
}
```

- [ ] **Step 3: Update ToDto to include ModelType**

```csharp
private static ModelRecordDto ToDto(ModelRecord r) =>
    new(r.Id, r.Title, r.Type, r.ModelFamily, r.Format, r.FilePath, r.FileSize,
        r.Source, r.Tags, r.Description, r.PreviewImagePath, r.CompatibilityHints,
        r.Status, r.DetectedAt);
```

- [ ] **Step 4: Run tests, verify pass**

Run: `dotnet test tests/StableDiffusionStudio.Application.Tests`

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: update ModelCatalogService for IModelProvider

Search dispatches to provider by ProviderId. Download enqueues background job.
GetProviders returns registered provider list with capabilities. ScanAsync
uses ScanLocalAsync and maps DiscoveredModel to ModelRecord with ModelType."
```

---

### Task 4: Refactor LocalFolderAdapter → LocalFolderProvider

**Files:**
- Rename: `src/StableDiffusionStudio.Infrastructure/ModelSources/LocalFolderAdapter.cs` → `LocalFolderProvider.cs`
- Modify: `tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/LocalFolderAdapterTests.cs` (rename + update)
- Modify: `src/StableDiffusionStudio.Web/Program.cs`

- [ ] **Step 1: Rename and implement IModelProvider**

Rename `LocalFolderAdapter` to `LocalFolderProvider`. Implement `IModelProvider`:
- `ProviderId = "local-folder"`, `DisplayName = "Local Folder"`
- `Capabilities = new(CanScanLocal: true, CanSearch: false, CanDownload: false, RequiresAuth: false, SupportedModelTypes: Enum.GetValues<ModelType>().ToList())`
- `ScanLocalAsync` returns `IReadOnlyList<DiscoveredModel>` instead of `ModelRecord` — map scan results to `DiscoveredModel`, include `ModelFileAnalyzer.InferModelType`
- `SearchAsync` returns `new SearchResult([], 0, false)`
- `DownloadAsync` returns `new DownloadResult(false, null, "Local provider does not support downloads")`
- `ValidateCredentialsAsync` returns `true`

- [ ] **Step 2: Update tests**

Rename test class and update assertions for `DiscoveredModel` return type. Add test for `ModelType` inference in scan results.

- [ ] **Step 3: Update DI registration**

In `Program.cs`, replace:
```csharp
builder.Services.AddScoped<IModelSourceAdapter, LocalFolderAdapter>();
```
with:
```csharp
builder.Services.AddScoped<IModelProvider, LocalFolderProvider>();
```

- [ ] **Step 4: Run full test suite, verify pass**

Run: `dotnet test --filter "FullyQualifiedName!~E2E"`

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: rename LocalFolderAdapter to LocalFolderProvider

Implements IModelProvider with capability-driven dispatch. ScanLocalAsync
returns DiscoveredModel with ModelType inference. Non-applicable methods
return safe defaults."
```

---

## Chunk 2: Infrastructure — Providers & Downloads (Tasks 5-8)

### Task 5: ProviderCredentialStore and HttpDownloadClient

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Settings/ProviderCredentialStore.cs`
- Create: `src/StableDiffusionStudio.Infrastructure/ModelSources/HttpDownloadClient.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/Settings/ProviderCredentialStoreTests.cs`

- [ ] **Step 1: Implement ProviderCredentialStore**

```csharp
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Settings;

public class ProviderCredentialStore : IProviderCredentialStore
{
    private readonly ISettingsProvider _settings;
    private const string KeyPrefix = "provider-credentials:";

    public ProviderCredentialStore(ISettingsProvider settings) { _settings = settings; }

    public Task<string?> GetTokenAsync(string providerId, CancellationToken ct = default)
        => _settings.GetAsync<string>($"{KeyPrefix}{providerId}", ct);

    public Task SetTokenAsync(string providerId, string token, CancellationToken ct = default)
        => _settings.SetAsync($"{KeyPrefix}{providerId}", token, ct);

    public Task RemoveTokenAsync(string providerId, CancellationToken ct = default)
        => _settings.SetAsync<string?>($"{KeyPrefix}{providerId}", null, ct);
}
```

- [ ] **Step 2: Implement HttpDownloadClient**

```csharp
using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Infrastructure.ModelSources;

public class HttpDownloadClient
{
    private readonly HttpClient _httpClient;

    public HttpDownloadClient(HttpClient httpClient) { _httpClient = httpClient; }

    public async Task<DownloadResult> DownloadFileAsync(
        string url, string targetPath, string? authToken,
        IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (authToken is not null)
                request.Headers.Authorization = new("Bearer", authToken);

            using var response = await _httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var dir = Path.GetDirectoryName(targetPath);
            if (dir is not null) Directory.CreateDirectory(dir);

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;
                progress.Report(new DownloadProgress(totalRead, totalBytes, "Downloading"));
            }

            return new DownloadResult(true, targetPath, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DownloadResult(false, null, ex.Message);
        }
    }
}
```

- [ ] **Step 3: Write credential store tests**

```csharp
[Fact]
public async Task SetAndGetToken_RoundTrips()
{
    await _store.SetTokenAsync("huggingface", "hf_test_token");
    var result = await _store.GetTokenAsync("huggingface");
    result.Should().Be("hf_test_token");
}

[Fact]
public async Task GetToken_MissingProvider_ReturnsNull()
{
    var result = await _store.GetTokenAsync("nonexistent");
    result.Should().BeNull();
}

[Fact]
public async Task RemoveToken_RemovesCredential()
{
    await _store.SetTokenAsync("civitai", "key123");
    await _store.RemoveTokenAsync("civitai");
    var result = await _store.GetTokenAsync("civitai");
    result.Should().BeNull();
}
```

- [ ] **Step 4: Run tests, commit**

```bash
git add -A
git commit -m "feat: add ProviderCredentialStore and HttpDownloadClient

ProviderCredentialStore stores auth tokens via ISettingsProvider.
HttpDownloadClient handles chunked streaming downloads with progress
reporting and auth token support."
```

---

### Task 6: HuggingFace Provider

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/ModelSources/HuggingFaceProvider.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/HuggingFaceProviderTests.cs`

- [ ] **Step 1: Write failing tests with mock HTTP responses**

Test search, download, and auth validation using mock HttpMessageHandler.

```csharp
public class HuggingFaceProviderTests
{
    [Fact]
    public async Task SearchAsync_ReturnsModelsFromApi()
    {
        // Mock HF API response for GET /api/models?search=sdxl&pipeline_tag=text-to-image
        // Assert mapped RemoteModelInfo fields
    }

    [Fact]
    public async Task SearchAsync_EmptyResult_ReturnsEmptyList() { ... }

    [Fact]
    public void Capabilities_CorrectlyDeclared()
    {
        var provider = CreateProvider();
        provider.Capabilities.CanSearch.Should().BeTrue();
        provider.Capabilities.CanDownload.Should().BeTrue();
        provider.Capabilities.CanScanLocal.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WithValidToken_ReturnsTrue() { ... }

    [Fact]
    public async Task ValidateCredentialsAsync_WithNoToken_ReturnsTrue() { ... }
}
```

- [ ] **Step 2: Implement HuggingFaceProvider**

```csharp
public class HuggingFaceProvider : IModelProvider
{
    private const string BaseUrl = "https://huggingface.co";
    private readonly HttpClient _httpClient;
    private readonly IProviderCredentialStore _credentials;
    private readonly HttpDownloadClient _downloadClient;

    public string ProviderId => "huggingface";
    public string DisplayName => "Hugging Face";
    public ModelProviderCapabilities Capabilities => new(
        CanScanLocal: false, CanSearch: true, CanDownload: true,
        RequiresAuth: false,
        SupportedModelTypes: new[] { ModelType.Checkpoint, ModelType.VAE, ModelType.LoRA, ModelType.Embedding, ModelType.ControlNet });

    // SearchAsync: GET /api/models?search={term}&pipeline_tag=text-to-image&sort=downloads&limit={pageSize}&offset={page*pageSize}
    // Map response JSON to RemoteModelInfo list
    // DownloadAsync: resolve URL as {BaseUrl}/{modelId}/resolve/main/{filename}, use HttpDownloadClient
    // ValidateCredentialsAsync: GET /api/whoami with bearer token
}
```

- [ ] **Step 3: Run tests, commit**

```bash
git add -A
git commit -m "feat: add HuggingFace model provider

Search via HF Hub API, download via resolve URLs, optional auth
token for gated models. Maps HF model metadata to RemoteModelInfo."
```

---

### Task 7: CivitAI Provider

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/ModelSources/CivitAIProvider.cs`
- Create: `tests/StableDiffusionStudio.Infrastructure.Tests/ModelSources/CivitAIProviderTests.cs`

- [ ] **Step 1: Write failing tests with mock HTTP responses**

Same pattern as HuggingFace — mock CivitAI API responses.

- [ ] **Step 2: Implement CivitAIProvider**

```csharp
public class CivitAIProvider : IModelProvider
{
    private const string BaseUrl = "https://civitai.com/api/v1";

    public string ProviderId => "civitai";
    public string DisplayName => "CivitAI";
    public ModelProviderCapabilities Capabilities => new(
        CanScanLocal: false, CanSearch: true, CanDownload: true,
        RequiresAuth: false,
        SupportedModelTypes: new[] { ModelType.Checkpoint, ModelType.VAE, ModelType.LoRA, ModelType.Embedding, ModelType.ControlNet });

    // SearchAsync: GET /models?query={term}&types={type}&sort={sort}&limit={pageSize}&page={page+1}
    // Map CivitAI types (Checkpoint, LORA, TextualInversion, VAE, etc.) to our ModelType
    // DownloadAsync: GET /download/models/{versionId}, use HttpDownloadClient
    // ValidateCredentialsAsync: test API key with authenticated endpoint
}
```

- [ ] **Step 3: Run tests, commit**

```bash
git add -A
git commit -m "feat: add CivitAI model provider

Search via CivitAI REST API, download model files by version,
optional API key auth. Maps CivitAI model types to ModelType enum."
```

---

### Task 8: ModelDownloadJobHandler

**Files:**
- Create: `src/StableDiffusionStudio.Infrastructure/Jobs/ModelDownloadJobHandler.cs`
- Modify: `src/StableDiffusionStudio.Web/Program.cs` — register handler

- [ ] **Step 1: Implement ModelDownloadJobHandler**

```csharp
public class ModelDownloadJobHandler : IJobHandler
{
    private readonly IEnumerable<IModelProvider> _providers;
    private readonly IModelCatalogRepository _catalogRepository;
    private readonly ILogger<ModelDownloadJobHandler> _logger;

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<DownloadRequest>(job.Data!);
        var provider = _providers.FirstOrDefault(p => p.ProviderId == request!.ProviderId);

        job.UpdateProgress(5, "Starting download");

        var progress = new Progress<DownloadProgress>(p =>
        {
            var pct = p.TotalBytes > 0 ? (int)(p.BytesDownloaded * 90 / p.TotalBytes) + 5 : 50;
            job.UpdateProgress(pct, p.Phase);
        });

        var result = await provider!.DownloadAsync(request!, progress, ct);

        if (result.Success && result.LocalFilePath is not null)
        {
            job.UpdateProgress(95, "Registering in catalog");
            // Create ModelRecord from download result and register in catalog
            var record = ModelRecord.Create(null, result.LocalFilePath,
                ModelFamily.Unknown, ModelFormat.Unknown, new FileInfo(result.LocalFilePath).Length,
                request!.ProviderId, request.Type);
            await _catalogRepository.UpsertAsync(record, ct);
            job.Complete($"Downloaded to {result.LocalFilePath}");
        }
        else
        {
            job.Fail(result.Error ?? "Download failed");
        }
    }
}
```

- [ ] **Step 2: Register in DI**

```csharp
builder.Services.AddKeyedScoped<IJobHandler, ModelDownloadJobHandler>("model-download");
```

- [ ] **Step 3: Run all tests, commit**

```bash
git add -A
git commit -m "feat: add ModelDownloadJobHandler for background downloads

Downloads models via IModelProvider, reports progress to JobRecord,
registers downloaded model in catalog on completion."
```

---

## Chunk 3: Presentation & Integration (Tasks 9-11)

### Task 9: Update Models Page with Provider Tabs and Download

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Components/Pages/Models.razor`
- Modify: `src/StableDiffusionStudio.Web/Components/Shared/ModelCard.razor`
- Create: `src/StableDiffusionStudio.Web/Components/Dialogs/DownloadModelDialog.razor`

- [ ] **Step 1: Add provider source tabs to Models page**

Add `MudTabs` at the top: "Local Catalog" (default), plus one tab per remote provider (from `GetProviders()`). When a remote tab is selected, search queries go to that provider. Local tab shows existing catalog behavior.

- [ ] **Step 2: Add remote search results rendering**

When a remote provider tab is active, show `RemoteModelInfo` cards with "Download" button instead of local model cards. Search triggers `SearchAsync` with the selected provider.

- [ ] **Step 3: Create DownloadModelDialog**

Shows model details, variant selection (if multiple files), target storage root selection, and "Download" button that calls `RequestDownloadAsync`.

- [ ] **Step 4: Update ModelCard for ModelType**

Add `ModelType` chip/badge to the card alongside family/format.

- [ ] **Step 5: Add ModelType filter**

Add `ModelType?` dropdown to the toolbar for filtering by Checkpoint/VAE/LoRA/etc.

- [ ] **Step 6: Verify in browser, commit**

```bash
git add -A
git commit -m "feat: update Models page with provider tabs, search, and download

Source tabs for Local, HuggingFace, CivitAI. Remote search with paginated
results. Download dialog with variant and target selection. ModelType
filter and badges on cards."
```

---

### Task 10: Update Settings Page with Provider Credentials

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Components/Pages/Settings.razor`

- [ ] **Step 1: Add Model Sources expansion panel**

List registered providers. For each provider that `RequiresAuth`, show:
- Token/API key input field (password masked)
- "Validate" button that calls `ValidateCredentialsAsync`
- Status indicator (connected / not configured)

- [ ] **Step 2: Wire to ProviderCredentialStore**

Save/load credentials via `IProviderCredentialStore`.

- [ ] **Step 3: Verify in browser, commit**

```bash
git add -A
git commit -m "feat: add provider credential management to Settings

Model Sources panel with token input, validation, and status
for HuggingFace and CivitAI providers."
```

---

### Task 11: Register All Providers in DI and Final Integration

**Files:**
- Modify: `src/StableDiffusionStudio.Web/Program.cs`
- Modify: `src/StableDiffusionStudio.Infrastructure/Persistence/Repositories/ModelCatalogRepository.cs` (add Type filter)

- [ ] **Step 1: Register all providers and services**

```csharp
// Model providers
builder.Services.AddScoped<IModelProvider, LocalFolderProvider>();
builder.Services.AddScoped<IModelProvider, HuggingFaceProvider>();
builder.Services.AddScoped<IModelProvider, CivitAIProvider>();
builder.Services.AddScoped<IProviderCredentialStore, ProviderCredentialStore>();
builder.Services.AddHttpClient<HuggingFaceProvider>();
builder.Services.AddHttpClient<CivitAIProvider>();
builder.Services.AddScoped<HttpDownloadClient>();
```

- [ ] **Step 2: Add ModelType filter to ModelCatalogRepository.ListAsync**

```csharp
if (filter.Type.HasValue)
    query = query.Where(m => m.Type == filter.Type.Value);
```

- [ ] **Step 3: Create EF migration**

```bash
dotnet ef migrations add AddModelType --project src/StableDiffusionStudio.Infrastructure --startup-project src/StableDiffusionStudio.Web
```

- [ ] **Step 4: Run full test suite**

```bash
dotnet test --filter "FullyQualifiedName!~E2E"
```

- [ ] **Step 5: Verify end-to-end in browser**

1. Launch via Aspire
2. Go to Models → see Local/HuggingFace/CivitAI tabs
3. Search HuggingFace for "stable diffusion"
4. See results with download buttons
5. Configure HF token in Settings
6. Download a model → see it in Jobs → appears in Local catalog

- [ ] **Step 6: Commit and push**

```bash
git add -A
git commit -m "feat: complete unified model provider integration

All providers registered in DI. ModelType filter in repository.
EF migration for ModelType column. Full end-to-end: search remote
providers, download models, auto-register in local catalog."
git push origin main
```

---

## Summary

| Task | Description | Key Outputs |
|------|-------------|-------------|
| 1 | ModelType enum + ModelRecord + ModelFileAnalyzer | Model type classification |
| 2 | IModelProvider interface + all DTOs | Unified provider contract |
| 3 | ModelCatalogService update | Search/download orchestration |
| 4 | LocalFolderProvider refactor | Backward-compatible local scanning |
| 5 | CredentialStore + HttpDownloadClient | Auth + download infrastructure |
| 6 | HuggingFace provider | Remote search + download |
| 7 | CivitAI provider | Remote search + download |
| 8 | ModelDownloadJobHandler | Background download execution |
| 9 | Models page update | Provider tabs, search, download UI |
| 10 | Settings page update | Credential management |
| 11 | Final integration | DI wiring, migration, E2E verification |
