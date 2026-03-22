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
        [ModelType.ControlNet] = "Controlnet",
    };

    private static readonly Dictionary<string, ModelType> ReverseCivitTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Checkpoint"] = ModelType.Checkpoint,
        ["LORA"] = ModelType.LoRA,
        ["VAE"] = ModelType.VAE,
        ["TextualInversion"] = ModelType.Embedding,
        ["Controlnet"] = ModelType.ControlNet,
    };

    private static readonly Dictionary<SortOrder, string> CivitSortMap = new()
    {
        [SortOrder.Relevance] = "Most Downloaded",
        [SortOrder.MostDownloaded] = "Most Downloaded",
        [SortOrder.Newest] = "Newest",
        [SortOrder.Name] = "Highest Rated",
    };

    private static readonly Dictionary<ModelFamily, string[]> CivitFamilyMap = new()
    {
        [ModelFamily.SD15] = ["SD 1.5"],
        [ModelFamily.SDXL] = ["SDXL 1.0"],
        [ModelFamily.Flux] = ["Flux.1 D", "Flux.1 S"],
        [ModelFamily.Pony] = ["Pony"],
        [ModelFamily.Illustrious] = ["Illustrious"],
    };

    /// <summary>All base model values supported by CivitAI's API, for the filter dropdown.</summary>
    public static readonly string[] AllBaseModels =
    [
        "Aura Flow", "Chroma", "CogVideoX",
        "Flux.1 D", "Flux.1 Frea", "Flux.1 Fontext", "Flux.1 S",
        "Flux.2 D", "Flux.2 Klein 9B", "Flux.2 Klein 9B-base", "Flux.2 Klein 4B", "Flux.2 Klein 4B-base",
        "HiDream", "Hunyuan 1", "Hunyuan Video",
        "Illustrious", "Kolors",
        "LTXV", "LTXV 2", "LTXV 2.3", "Lumina",
        "Mochi", "NoobAI",
        "Pony", "Pony V7",
        "QWen",
        "SD 1.5", "SD 3", "SD 3.5",
        "SDXL 1.0", "SDXL Hyper", "SDXL Lightning", "SDXL Turbo",
        "Stable Audio", "SVD",
        "Wan Video 1.3B T2V", "Wan Video 14B T2V",
        "Z Image Turbo",
        "Other"
    ];

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
            var sortParam = CivitSortMap.GetValueOrDefault(query.Sort, "Most Downloaded");
            var url = $"{ApiBaseUrl}/models?sort={Uri.EscapeDataString(sortParam)}&limit={query.PageSize}&page={page}";

            // Only add query param if search term is non-empty — CivitAI returns
            // popular models when query is omitted, but returns nothing for empty string
            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
                url += $"&query={Uri.EscapeDataString(query.SearchTerm)}";

            if (query.Type.HasValue && CivitTypeMap.TryGetValue(query.Type.Value, out var civitType))
                url += $"&types={civitType}";

            // Use explicit BaseModel filter if provided (from CivitAI-specific dropdown)
            if (!string.IsNullOrWhiteSpace(query.BaseModel))
            {
                url += $"&baseModels={Uri.EscapeDataString(query.BaseModel)}";
            }
            // Otherwise fall back to Family enum mapping
            else if (query.Family.HasValue && CivitFamilyMap.TryGetValue(query.Family.Value, out var baseModels))
            {
                foreach (var bm in baseModels)
                    url += $"&baseModels={Uri.EscapeDataString(bm)}";
            }

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

        var providerUrl = $"https://civitai.com/models/{modelId}";

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
                    if (baseModelStr.Contains("pony")) family = ModelFamily.Pony;
                    else if (baseModelStr.Contains("illustrious") || baseModelStr.Contains("noobai")) family = ModelFamily.Illustrious;
                    else if (baseModelStr.Contains("flux")) family = ModelFamily.Flux;
                    else if (baseModelStr.Contains("sdxl") || baseModelStr.Contains("xl")) family = ModelFamily.SDXL;
                    else if (baseModelStr.Contains("sd 1") || baseModelStr.Contains("sd1") || baseModelStr.Contains("1.5")) family = ModelFamily.SD15;
                }

                // Extract trained words into tags
                if (firstVersion.TryGetProperty("trainedWords", out var trainedWords) &&
                    trainedWords.ValueKind == JsonValueKind.Array)
                {
                    foreach (var word in trainedWords.EnumerateArray())
                    {
                        var w = word.GetString();
                        if (!string.IsNullOrEmpty(w) && !tags.Contains(w, StringComparer.OrdinalIgnoreCase))
                            tags.Add(w);
                    }
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
            ProviderUrl: providerUrl,
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
