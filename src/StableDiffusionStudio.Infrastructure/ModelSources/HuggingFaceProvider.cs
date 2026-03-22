using System.Net.Http.Headers;
using System.Text.Json;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.ModelSources;

public class HuggingFaceProvider : IModelProvider
{
    private readonly HttpClient _httpClient;
    private readonly HttpDownloadClient _downloadClient;
    private readonly IProviderCredentialStore _credentialStore;

    private const string ApiBaseUrl = "https://huggingface.co/api";
    private const string SiteBaseUrl = "https://huggingface.co";

    public HuggingFaceProvider(
        HttpClient httpClient,
        HttpDownloadClient downloadClient,
        IProviderCredentialStore credentialStore)
    {
        _httpClient = httpClient;
        _downloadClient = downloadClient;
        _credentialStore = credentialStore;
    }

    public string ProviderId => "huggingface";
    public string DisplayName => "Hugging Face";

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
            var offset = query.Page * query.PageSize;
            var url = $"{ApiBaseUrl}/models?search={Uri.EscapeDataString(query.SearchTerm ?? "")}" +
                      $"&pipeline_tag=text-to-image&library=diffusers&sort=downloads" +
                      $"&limit={query.PageSize}&offset={offset}" +
                      $"&expand[]=siblings";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var token = await _credentialStore.GetTokenAsync(ProviderId, ct);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var models = JsonSerializer.Deserialize<JsonElement[]>(json) ?? [];

            var results = new List<RemoteModelInfo>();
            foreach (var model in models)
            {
                var id = model.GetProperty("id").GetString() ?? "";
                var title = id.Contains('/') ? id.Split('/').Last() : id;
                var tags = ParseTags(model);
                var family = InferFamilyFromModel(id, tags);
                var modelType = InferTypeFromTags(tags);

                // Extract file variants from siblings
                var variants = new List<ModelFileVariant>();
                if (model.TryGetProperty("siblings", out var siblings) && siblings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sibling in siblings.EnumerateArray())
                    {
                        var rfilename = sibling.TryGetProperty("rfilename", out var rfn) ? rfn.GetString() ?? "" : "";
                        if (IsModelFile(rfilename))
                        {
                            var format = InferFormatFromFileName(rfilename);
                            var size = sibling.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number
                                ? sz.GetInt64() : 0L;
                            var quantization = rfilename.Contains("q4", StringComparison.OrdinalIgnoreCase) ? "Q4"
                                : rfilename.Contains("q5", StringComparison.OrdinalIgnoreCase) ? "Q5"
                                : rfilename.Contains("q8", StringComparison.OrdinalIgnoreCase) ? "Q8"
                                : null;
                            variants.Add(new ModelFileVariant(rfilename, size, format, quantization));
                        }
                    }
                }

                var primaryFormat = variants.Count > 0 ? variants[0].Format : ModelFormat.SafeTensors;
                var primarySize = variants.Count > 0 && variants[0].FileSize > 0 ? (long?)variants[0].FileSize : null;

                // Resolve preview image URL
                var previewUrl = ResolvePreviewUrl(id, model);

                results.Add(new RemoteModelInfo(
                    ExternalId: id,
                    Title: title,
                    Description: TruncateDescription(model),
                    Type: modelType,
                    Family: family,
                    Format: primaryFormat,
                    FileSize: primarySize,
                    PreviewImageUrl: previewUrl,
                    Tags: tags,
                    ProviderUrl: $"{SiteBaseUrl}/{id}",
                    Variants: variants));
            }

            // Client-side family filter (HF API doesn't support filtering by base model family)
            if (query.Family.HasValue)
                results = results.Where(r => r.Family == query.Family.Value).ToList();

            return new SearchResult(results, results.Count, results.Count == query.PageSize);
        }
        catch (HttpRequestException)
        {
            return new SearchResult([], 0, false);
        }
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        var fileName = request.VariantFileName ?? "model.safetensors";
        var url = $"{SiteBaseUrl}/{request.ExternalId}/resolve/main/{fileName}";
        var targetPath = Path.Combine(request.TargetRoot.Path, fileName);
        var token = await _credentialStore.GetTokenAsync(ProviderId, ct);

        return await _downloadClient.DownloadFileAsync(url, targetPath, string.IsNullOrEmpty(token) ? null : token, progress, ct);
    }

    public async Task<bool> ValidateCredentialsAsync(CancellationToken ct = default)
    {
        var token = await _credentialStore.GetTokenAsync(ProviderId, ct);
        if (string.IsNullOrEmpty(token))
            return true; // Public access works without auth

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBaseUrl}/whoami");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ParseTags(JsonElement model)
    {
        if (model.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            return tagsElement.EnumerateArray().Select(t => t.GetString() ?? "").Where(t => t.Length > 0).ToList();
        return [];
    }

    private static string? TruncateDescription(JsonElement model)
    {
        if (model.TryGetProperty("cardData", out var cardData) &&
            cardData.TryGetProperty("description", out var desc))
        {
            var text = desc.GetString();
            if (text is not null && text.Length > 200)
                return text[..200];
            return text;
        }
        return null;
    }

    private static ModelFamily InferFamilyFromModel(string modelId, IReadOnlyList<string> tags)
    {
        var combined = modelId.ToLowerInvariant() + " " + string.Join(" ", tags).ToLowerInvariant();
        if (combined.Contains("flux")) return ModelFamily.Flux;
        if (combined.Contains("sdxl") || combined.Contains("stable-diffusion-xl")) return ModelFamily.SDXL;
        if (combined.Contains("sd-1") || combined.Contains("sd1") || combined.Contains("stable-diffusion-v1")) return ModelFamily.SD15;
        return ModelFamily.Unknown;
    }

    private static ModelType InferTypeFromTags(IReadOnlyList<string> tags)
    {
        var lower = tags.Select(t => t.ToLowerInvariant()).ToHashSet();
        if (lower.Contains("lora")) return ModelType.LoRA;
        if (lower.Contains("textual-inversion") || lower.Contains("embedding")) return ModelType.Embedding;
        if (lower.Contains("controlnet")) return ModelType.ControlNet;
        if (lower.Contains("vae")) return ModelType.VAE;
        return ModelType.Checkpoint;
    }

    private static bool IsModelFile(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext is ".safetensors" or ".ckpt" or ".gguf" or ".bin" or ".pt";
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

    private static string? ResolvePreviewUrl(string modelId, JsonElement model)
    {
        // Check for card thumbnail in cardData
        if (model.TryGetProperty("cardData", out var cardData) &&
            cardData.TryGetProperty("thumbnail", out var thumb))
        {
            var thumbnailUrl = thumb.GetString();
            if (!string.IsNullOrEmpty(thumbnailUrl))
                return thumbnailUrl;
        }

        // Fallback: resolve from known image patterns in siblings
        if (model.TryGetProperty("siblings", out var siblings) && siblings.ValueKind == JsonValueKind.Array)
        {
            foreach (var sibling in siblings.EnumerateArray())
            {
                var rfilename = sibling.TryGetProperty("rfilename", out var rfn) ? rfn.GetString() ?? "" : "";
                if (IsImageFile(rfilename))
                    return $"{SiteBaseUrl}/{modelId}/resolve/main/{rfilename}";
            }
        }

        return null;
    }

    private static bool IsImageFile(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif";
    }
}
