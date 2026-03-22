using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Enriches local model records by looking them up on CivitAI.
/// Matches by filename search, downloads preview images and metadata.
/// Implements <see cref="IModelEnrichmentProvider"/> for the new enrichment pipeline.
/// </summary>
public class CivitAIEnrichmentProvider : IModelEnrichmentProvider
{
    private readonly HttpClient _httpClient;
    private readonly IProviderCredentialStore _credentialStore;
    private readonly ILogger<CivitAIEnrichmentProvider> _logger;

    private const string ApiBaseUrl = "https://civitai.com/api/v1";

    public CivitAIEnrichmentProvider(
        HttpClient httpClient,
        IProviderCredentialStore credentialStore,
        ILogger<CivitAIEnrichmentProvider> logger)
    {
        _httpClient = httpClient;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    public string ProviderId => "civitai";
    public string DisplayName => "CivitAI";

    public async Task<ModelEnrichmentResult?> EnrichAsync(ModelRecord model, CancellationToken ct = default)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(model.FilePath);
            _logger.LogDebug("Enriching metadata for {Model} (searching by: {FileName})", model.Title, fileName);

            // Search CivitAI by the model filename
            var url = $"{ApiBaseUrl}/models?query={Uri.EscapeDataString(fileName)}&limit=5";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var token = await _credentialStore.GetTokenAsync("civitai", ct);
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CivitAI search returned {StatusCode} for {Model}", response.StatusCode, model.Title);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            if (!doc.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return null;

            // Try to find a match by comparing file names and sizes
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("modelVersions", out var versions) ||
                    versions.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var version in versions.EnumerateArray())
                {
                    if (!version.TryGetProperty("files", out var files) ||
                        files.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var file in files.EnumerateArray())
                    {
                        var remoteFileName = file.TryGetProperty("name", out var fn) ? fn.GetString() ?? "" : "";
                        var remoteSize = file.TryGetProperty("sizeKB", out var sz) ? (long)(sz.GetDouble() * 1024) : 0;

                        // Match: same filename, or file size within 1% tolerance
                        var localFileName = Path.GetFileName(model.FilePath);
                        var nameMatch = string.Equals(remoteFileName, localFileName, StringComparison.OrdinalIgnoreCase);
                        var sizeMatch = model.FileSize > 0 && remoteSize > 0 &&
                                        Math.Abs(model.FileSize - remoteSize) < model.FileSize * 0.01;

                        if (!nameMatch && !sizeMatch)
                            continue;

                        _logger.LogInformation("Matched {Model} to CivitAI model by {MatchType}",
                            model.Title, nameMatch ? "filename" : "filesize");

                        // Extract metadata from this match
                        return await BuildEnrichmentResultAsync(model, item, version, ct);
                    }
                }
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error enriching {Model} from CivitAI", model.Title);
            return null;
        }
    }

    private async Task<ModelEnrichmentResult?> BuildEnrichmentResultAsync(
        ModelRecord model,
        JsonElement item,
        JsonElement version,
        CancellationToken ct)
    {
        string? previewUrl = null;
        string? description = null;
        string? remoteModelId = null;
        string? providerUrl = null;

        // Extract model ID and provider URL
        if (item.TryGetProperty("id", out var idProp))
        {
            remoteModelId = idProp.GetInt32().ToString();
            providerUrl = $"https://civitai.com/models/{remoteModelId}";
        }

        // Extract preview image URL
        if (version.TryGetProperty("images", out var images) &&
            images.ValueKind == JsonValueKind.Array)
        {
            var firstImage = images.EnumerateArray().FirstOrDefault();
            if (firstImage.ValueKind == JsonValueKind.Object &&
                firstImage.TryGetProperty("url", out var imgUrl))
            {
                previewUrl = imgUrl.GetString();
            }
        }

        // Extract description (strip HTML)
        if (item.TryGetProperty("description", out var descProp))
        {
            var text = descProp.GetString();
            if (text is not null)
            {
                description = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
                if (description.Length > 500) description = description[..500];
            }
        }

        // Extract tags from item tags + trained words
        var tagList = new List<string>();
        if (item.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            tagList.AddRange(tagsProp.EnumerateArray()
                .Select(t => t.GetString() ?? "")
                .Where(t => t.Length > 0));
        }
        if (version.TryGetProperty("trainedWords", out var trainedWords) &&
            trainedWords.ValueKind == JsonValueKind.Array)
        {
            tagList.AddRange(trainedWords.EnumerateArray()
                .Select(t => t.GetString() ?? "")
                .Where(t => t.Length > 0 && !tagList.Contains(t, StringComparer.OrdinalIgnoreCase)));
        }

        // Download preview image to disk alongside the model file
        string? savedPreviewPath = null;
        if (!string.IsNullOrEmpty(previewUrl))
        {
            savedPreviewPath = await DownloadPreviewImageAsync(model, previewUrl, ct);
        }

        // Set cross-link fields on the model
        if (remoteModelId is not null)
        {
            model.UpdateEnrichment(civitAIModelId: remoteModelId, civitAIUrl: providerUrl);
        }

        // Only return a result if we extracted something useful
        if (savedPreviewPath is null && description is null && tagList.Count == 0 && remoteModelId is null)
            return null;

        _logger.LogInformation(
            "Enriched {Model}: preview={HasPreview}, description={HasDesc}, tags={TagCount}, civitId={CivitId}",
            model.Title, savedPreviewPath is not null, description is not null, tagList.Count, remoteModelId);

        // Extract title from the item name
        string? title = null;
        if (item.TryGetProperty("name", out var nameProp))
        {
            title = nameProp.GetString();
        }

        return new ModelEnrichmentResult
        {
            ProviderId = ProviderId,
            RemoteModelId = remoteModelId,
            ProviderUrl = providerUrl,
            PreviewImagePath = savedPreviewPath,
            Description = description,
            Tags = tagList.Count > 0 ? tagList : null,
            Title = title
        };
    }

    private async Task<string?> DownloadPreviewImageAsync(
        ModelRecord model, string imageUrl, CancellationToken ct)
    {
        try
        {
            // Save preview alongside the model file (same convention as LocalFolderProvider)
            var modelDir = Path.GetDirectoryName(model.FilePath);
            if (modelDir is null) return null;

            var baseName = Path.GetFileNameWithoutExtension(model.FilePath);
            var previewPath = Path.Combine(modelDir, $"{baseName}.preview.jpeg");

            if (File.Exists(previewPath))
                return previewPath;

            // Also check for other extensions that may already exist
            var existingPreview = new[] { ".jpeg", ".png", ".webp" }
                .Select(ext => Path.Combine(modelDir, $"{baseName}.preview{ext}"))
                .FirstOrDefault(File.Exists);
            if (existingPreview is not null)
                return existingPreview;

            using var response = await _httpClient.GetAsync(imageUrl, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            // Detect actual content type to choose correct extension
            var contentType = response.Content.Headers.ContentType?.MediaType;
            string ext2;
            if (contentType?.Contains("png") == true) ext2 = ".png";
            else if (contentType?.Contains("webp") == true) ext2 = ".webp";
            else ext2 = ".jpeg";
            previewPath = Path.Combine(modelDir, $"{baseName}.preview{ext2}");

            Directory.CreateDirectory(modelDir);
            await using var fileStream = File.Create(previewPath);
            await response.Content.CopyToAsync(fileStream, ct);

            _logger.LogDebug("Downloaded preview image to {Path}", previewPath);
            return previewPath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to download preview image for {Model}", model.Title);
            return null;
        }
    }
}
