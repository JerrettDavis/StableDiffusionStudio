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
                      $"&limit={query.PageSize}&offset={offset}";

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

                results.Add(new RemoteModelInfo(
                    ExternalId: id,
                    Title: title,
                    Description: TruncateDescription(model),
                    Type: ModelType.Checkpoint,
                    Family: family,
                    Format: ModelFormat.SafeTensors,
                    FileSize: null,
                    PreviewImageUrl: null,
                    Tags: tags,
                    ProviderUrl: $"{SiteBaseUrl}/{id}",
                    Variants: []));
            }

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
}
