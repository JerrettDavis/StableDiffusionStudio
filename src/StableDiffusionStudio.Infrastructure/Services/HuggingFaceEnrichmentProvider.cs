using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Enriches local model records by looking them up on Hugging Face.
/// Matches by filename search against HF model repositories, downloads preview images and metadata.
/// </summary>
public class HuggingFaceEnrichmentProvider : IModelEnrichmentProvider
{
    private readonly HttpClient _httpClient;
    private readonly IProviderCredentialStore _credentialStore;
    private readonly IAppPaths _appPaths;
    private readonly ILogger<HuggingFaceEnrichmentProvider> _logger;

    private const string ApiBaseUrl = "https://huggingface.co/api";
    private const string SiteBaseUrl = "https://huggingface.co";

    public HuggingFaceEnrichmentProvider(
        HttpClient httpClient,
        IProviderCredentialStore credentialStore,
        IAppPaths appPaths,
        ILogger<HuggingFaceEnrichmentProvider> logger)
    {
        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _appPaths = appPaths;
        _logger = logger;
    }

    public string ProviderId => "huggingface";
    public string DisplayName => "Hugging Face";

    public async Task<ModelEnrichmentResult?> EnrichAsync(ModelRecord model, CancellationToken ct = default)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(model.FilePath);
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            _logger.LogDebug("Enriching metadata for {Model} via Hugging Face (searching by: {FileName})",
                model.Title, fileName);

            var searchUrl = $"{ApiBaseUrl}/models?search={Uri.EscapeDataString(fileName)}" +
                            "&pipeline_tag=text-to-image&limit=5";

            using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            var token = await _credentialStore.GetTokenAsync("huggingface", ct);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var models = JsonSerializer.Deserialize<JsonElement[]>(json) ?? [];

            if (models.Length == 0)
            {
                _logger.LogDebug("No Hugging Face results found for {FileName}", fileName);
                return null;
            }

            var normalizedLocal = fileName.ToLowerInvariant();

            foreach (var hfModel in models)
            {
                var modelId = hfModel.TryGetProperty("id", out var idProp)
                    ? idProp.GetString() ?? ""
                    : "";

                if (string.IsNullOrEmpty(modelId))
                    continue;

                // Extract the repo name (last segment of "user/model-name")
                var repoName = modelId.Contains('/')
                    ? modelId.Split('/').Last()
                    : modelId;

                var normalizedRepo = repoName.ToLowerInvariant();

                // Fuzzy match: compare stripped local filename against repo name
                if (!IsFuzzyMatch(normalizedLocal, normalizedRepo))
                    continue;

                _logger.LogInformation("Matched {Model} to Hugging Face repo {RepoId}",
                    model.Title, modelId);

                return await ExtractEnrichmentAsync(model, hfModel, modelId, token, ct);
            }

            _logger.LogDebug("No Hugging Face match found for {Model}", model.Title);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error while enriching {Model} from Hugging Face", model.Title);
            return null;
        }
    }

    private static bool IsFuzzyMatch(string normalizedLocal, string normalizedRepo)
    {
        // Exact match
        if (normalizedLocal == normalizedRepo)
            return true;

        // Check if one contains the other
        if (normalizedLocal.Contains(normalizedRepo) || normalizedRepo.Contains(normalizedLocal))
            return true;

        // Strip common suffixes/separators and compare
        var strippedLocal = StripModelSuffixes(normalizedLocal);
        var strippedRepo = StripModelSuffixes(normalizedRepo);

        if (strippedLocal == strippedRepo)
            return true;

        if (strippedLocal.Length > 3 && strippedRepo.Length > 3 &&
            (strippedLocal.Contains(strippedRepo) || strippedRepo.Contains(strippedLocal)))
            return true;

        return false;
    }

    private static string StripModelSuffixes(string name)
    {
        // Remove common model file suffixes like _fp16, -v1, _safetensors, etc.
        var stripped = name
            .Replace("-", "")
            .Replace("_", "")
            .Replace(".", "")
            .Replace(" ", "");

        // Remove common quantization/precision suffixes
        foreach (var suffix in new[] { "fp16", "fp32", "bf16", "q4", "q5", "q8", "safetensors", "ckpt", "gguf" })
        {
            if (stripped.EndsWith(suffix))
                stripped = stripped[..^suffix.Length];
        }

        return stripped;
    }

    private async Task<ModelEnrichmentResult?> ExtractEnrichmentAsync(
        ModelRecord model,
        JsonElement hfModel,
        string modelId,
        string? token,
        CancellationToken ct)
    {
        // Extract description from cardData
        string? description = null;
        if (hfModel.TryGetProperty("cardData", out var cardData) &&
            cardData.TryGetProperty("description", out var descProp))
        {
            var text = descProp.GetString();
            if (text is not null)
            {
                description = text.Length > 500 ? text[..500] : text;
            }
        }

        // Extract tags
        List<string>? tags = null;
        if (hfModel.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            tags = tagsProp.EnumerateArray()
                .Select(t => t.GetString() ?? "")
                .Where(t => t.Length > 0)
                .ToList();
        }

        // Resolve preview image URL
        var previewUrl = ResolvePreviewUrl(modelId, hfModel);

        // Download preview image to disk alongside the model file
        string? savedPreviewPath = null;
        if (!string.IsNullOrEmpty(previewUrl))
        {
            savedPreviewPath = await DownloadPreviewImageAsync(model, previewUrl, token, ct);
        }

        // Derive a title from the model ID
        var title = modelId.Contains('/')
            ? modelId.Split('/').Last()
            : modelId;

        var providerUrl = $"{SiteBaseUrl}/{modelId}";

        return new ModelEnrichmentResult
        {
            ProviderId = ProviderId,
            RemoteModelId = modelId,
            ProviderUrl = providerUrl,
            PreviewImagePath = savedPreviewPath,
            Description = description,
            Tags = tags,
            Title = title
        };
    }

    private static string? ResolvePreviewUrl(string modelId, JsonElement hfModel)
    {
        // Check for card thumbnail in cardData
        if (hfModel.TryGetProperty("cardData", out var cardData) &&
            cardData.TryGetProperty("thumbnail", out var thumb))
        {
            var thumbnailUrl = thumb.GetString();
            if (!string.IsNullOrEmpty(thumbnailUrl))
                return thumbnailUrl;
        }

        // Fallback: resolve from known image patterns in siblings
        if (hfModel.TryGetProperty("siblings", out var siblings) && siblings.ValueKind == JsonValueKind.Array)
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

    private async Task<string?> DownloadPreviewImageAsync(
        ModelRecord model, string imageUrl, string? token, CancellationToken ct)
    {
        try
        {
            var modelDir = Path.GetDirectoryName(model.FilePath);
            if (modelDir is null)
                return null;

            var baseName = Path.GetFileNameWithoutExtension(model.FilePath);
            var previewPath = Path.Combine(modelDir, $"{baseName}.preview.jpeg");

            // Skip if preview already exists
            if (File.Exists(previewPath))
            {
                _logger.LogDebug("Preview image already exists at {Path}", previewPath);
                return previewPath;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Failed to download preview image from {Url}: {StatusCode}",
                    imageUrl, response.StatusCode);
                return null;
            }

            Directory.CreateDirectory(modelDir);
            await using var fileStream = File.Create(previewPath);
            await response.Content.CopyToAsync(fileStream, ct);

            _logger.LogDebug("Downloaded Hugging Face preview image to {Path}", previewPath);
            return previewPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download preview image for {Model} from Hugging Face", model.Title);
            return null;
        }
    }
}
