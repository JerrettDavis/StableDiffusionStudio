using System.Net.Http.Headers;
using System.Text.Json;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.ModelSources;

public class CivitAIProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly HttpDownloadClient _downloadClient;
    private readonly IProviderCredentialStore _credentialStore;

    private const string ApiBaseUrl = "https://civitai.com/api/v1";

    private static readonly Dictionary<ModelType, string> CivitTypeMap = new()
    {
        [ModelType.Checkpoint] = "Checkpoint",
        [ModelType.LoRA] = "LORA",
        [ModelType.VAE] = "VAE",
        [ModelType.Embedding] = "TextualInversion",
    };

    private static readonly Dictionary<string, ModelType> ReverseCivitTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Checkpoint"] = ModelType.Checkpoint,
        ["LORA"] = ModelType.LoRA,
        ["VAE"] = ModelType.VAE,
        ["TextualInversion"] = ModelType.Embedding,
    };

    public CivitAIProvider(
        HttpClient httpClient,
        HttpDownloadClient downloadClient,
        IProviderCredentialStore credentialStore)
    {
        _httpClient = httpClient;
        _downloadClient = downloadClient;
        _credentialStore = credentialStore;
    }

    public string ProviderId => "civitai";
    public string DisplayName => "CivitAI";

    public ModelProviderCapabilities Capabilities => new(
        CanScanLocal: false,
        CanSearch: true,
        CanDownload: true,
        RequiresAuth: false,
        SupportedModelTypes: [ModelType.Checkpoint, ModelType.VAE, ModelType.LoRA, ModelType.Embedding, ModelType.ControlNet]);

    public Task<IReadOnlyList<DiscoveredModel>> ScanLocalAsync(StorageRoot root, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DiscoveredModel>>([]);

    public async Task<SearchResult> SearchAsync(ModelSearchQuery query, CancellationToken ct = default)
    {
        try
        {
            var page = query.Page + 1; // CivitAI uses 1-based pages
            var url = $"{ApiBaseUrl}/models?query={Uri.EscapeDataString(query.SearchTerm ?? "")}" +
                      $"&sort=Most%20Downloaded&limit={query.PageSize}&page={page}";

            if (query.Type.HasValue && CivitTypeMap.TryGetValue(query.Type.Value, out var civitType))
                url += $"&types={civitType}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var token = await _credentialStore.GetTokenAsync(ProviderId, ct);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            var results = new List<RemoteModelInfo>();

            if (doc.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var model in items.EnumerateArray())
                {
                    var mapped = MapCivitModel(model);
                    if (mapped is not null)
                        results.Add(mapped);
                }
            }

            var metadata = doc.TryGetProperty("metadata", out var meta) ? meta : default;
            var totalCount = metadata.ValueKind == JsonValueKind.Object &&
                             metadata.TryGetProperty("totalItems", out var total)
                ? total.GetInt32()
                : results.Count;
            var hasMore = metadata.ValueKind == JsonValueKind.Object &&
                          metadata.TryGetProperty("currentPage", out var currentPage) &&
                          metadata.TryGetProperty("totalPages", out var totalPages) &&
                          currentPage.GetInt32() < totalPages.GetInt32();

            return new SearchResult(results, totalCount, hasMore);
        }
        catch (HttpRequestException)
        {
            return new SearchResult([], 0, false);
        }
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        var url = $"{ApiBaseUrl}/download/models/{request.ExternalId}";
        var token = await _credentialStore.GetTokenAsync(ProviderId, ct);
        if (!string.IsNullOrEmpty(token))
            url += $"?token={Uri.EscapeDataString(token)}";

        var fileName = request.VariantFileName ?? $"civitai-{request.ExternalId}.safetensors";
        var targetPath = Path.Combine(request.TargetRoot.Path, fileName);

        // CivitAI uses query param auth, not bearer header for downloads
        return await _downloadClient.DownloadFileAsync(url, targetPath, null, progress, ct);
    }

    public async Task<bool> ValidateCredentialsAsync(CancellationToken ct = default)
    {
        var token = await _credentialStore.GetTokenAsync(ProviderId, ct);
        if (string.IsNullOrEmpty(token))
            return true; // Public access works without auth

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/models?limit=1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static RemoteModelInfo? MapCivitModel(JsonElement model)
    {
        var name = model.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
        var modelId = model.TryGetProperty("id", out var idProp) ? idProp.GetInt32().ToString() : "";
        var description = model.TryGetProperty("description", out var descProp)
            ? TruncateHtml(descProp.GetString(), 200)
            : null;

        var typeStr = model.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "" : "";
        var modelType = ReverseCivitTypeMap.GetValueOrDefault(typeStr, ModelType.Checkpoint);

        var tags = model.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array
            ? tagsProp.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t.Length > 0).ToList()
            : new List<string>();

        // Extract version info
        string externalId = modelId;
        string? previewUrl = null;
        var variants = new List<ModelFileVariant>();
        var family = ModelFamily.Unknown;
        var format = ModelFormat.SafeTensors;

        if (model.TryGetProperty("modelVersions", out var versions) &&
            versions.ValueKind == JsonValueKind.Array)
        {
            var firstVersion = versions.EnumerateArray().FirstOrDefault();
            if (firstVersion.ValueKind == JsonValueKind.Object)
            {
                if (firstVersion.TryGetProperty("id", out var versionId))
                    externalId = versionId.GetInt32().ToString();

                // Get base model for family inference
                if (firstVersion.TryGetProperty("baseModel", out var baseModel))
                {
                    var baseModelStr = baseModel.GetString()?.ToLowerInvariant() ?? "";
                    if (baseModelStr.Contains("flux")) family = ModelFamily.Flux;
                    else if (baseModelStr.Contains("sdxl") || baseModelStr.Contains("xl")) family = ModelFamily.SDXL;
                    else if (baseModelStr.Contains("sd 1") || baseModelStr.Contains("sd1") || baseModelStr.Contains("1.5")) family = ModelFamily.SD15;
                }

                // Get preview image
                if (firstVersion.TryGetProperty("images", out var images) &&
                    images.ValueKind == JsonValueKind.Array)
                {
                    var firstImage = images.EnumerateArray().FirstOrDefault();
                    if (firstImage.ValueKind == JsonValueKind.Object &&
                        firstImage.TryGetProperty("url", out var imgUrl))
                    {
                        previewUrl = imgUrl.GetString();
                    }
                }

                // Get file variants
                if (firstVersion.TryGetProperty("files", out var files) &&
                    files.ValueKind == JsonValueKind.Array)
                {
                    foreach (var file in files.EnumerateArray())
                    {
                        var fileName = file.TryGetProperty("name", out var fn) ? fn.GetString() ?? "" : "";
                        var fileSize = file.TryGetProperty("sizeKB", out var sz) ? (long)(sz.GetDouble() * 1024) : 0;
                        var fileFormat = InferFormatFromFileName(fileName);

                        string? quantization = null;
                        if (file.TryGetProperty("metadata", out var fileMeta) &&
                            fileMeta.TryGetProperty("fp", out var fp))
                        {
                            quantization = fp.GetString();
                        }

                        variants.Add(new ModelFileVariant(fileName, fileSize, fileFormat, quantization));

                        // Use the first file's ID as ExternalId for downloads
                        if (file.TryGetProperty("id", out var fileId) && variants.Count == 1)
                            externalId = fileId.GetInt32().ToString();
                    }

                    if (variants.Count > 0)
                        format = variants[0].Format;
                }
            }
        }

        return new RemoteModelInfo(
            ExternalId: externalId,
            Title: name,
            Description: description,
            Type: modelType,
            Family: family,
            Format: format,
            FileSize: variants.FirstOrDefault()?.FileSize,
            PreviewImageUrl: previewUrl,
            Tags: tags,
            ProviderUrl: $"https://civitai.com/models/{modelId}",
            Variants: variants);
    }

    private static ModelFormat InferFormatFromFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".safetensors" => ModelFormat.SafeTensors,
            ".ckpt" => ModelFormat.CKPT,
            ".gguf" => ModelFormat.GGUF,
            _ => ModelFormat.Unknown
        };
    }

    private static string? TruncateHtml(string? text, int maxLength)
    {
        if (text is null) return null;
        // Strip basic HTML tags for description
        var stripped = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
        return stripped.Length > maxLength ? stripped[..maxLength] : stripped;
    }
}
