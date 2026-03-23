using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Web.Mcp.Tools;

[McpServerToolType]
public class ModelTools
{
    [McpServerTool(Name = "search_models"), Description(
        "Search for models on CivitAI or HuggingFace. Returns model names, types, families, and download info.")]
    public static async Task<string> SearchModels(
        IModelCatalogService catalogService,
        [Description("Search term (e.g. 'realistic', 'anime', 'flux')")] string? searchTerm = null,
        [Description("Provider: 'civitai' or 'huggingface'")] string provider = "civitai",
        [Description("Model type filter: Checkpoint, LoRA, VAE, Embedding, ControlNet")] string? type = null,
        [Description("Model family filter: SD15, SDXL, Flux, Pony, Illustrious")] string? family = null,
        [Description("CivitAI base model filter (e.g. 'Flux.1 D', 'Pony', 'SDXL 1.0')")] string? baseModel = null,
        [Description("Sort: Relevance, Newest, MostDownloaded, Name")] string sort = "MostDownloaded",
        [Description("Page number (0-based)")] int page = 0)
    {
        var typeFilter = type is not null && Enum.TryParse<ModelType>(type, true, out var t) ? t : (ModelType?)null;
        var familyFilter = family is not null && Enum.TryParse<ModelFamily>(family, true, out var f) ? f : (ModelFamily?)null;
        var sortOrder = Enum.TryParse<SortOrder>(sort, true, out var so) ? so : SortOrder.MostDownloaded;

        var query = new ModelSearchQuery(provider, searchTerm, typeFilter, familyFilter, Sort: sortOrder, Page: page, BaseModel: baseModel);
        var result = await catalogService.SearchAsync(query);

        return JsonSerializer.Serialize(new
        {
            models = result.Models.Select(m => new
            {
                externalId = m.ExternalId,
                title = m.Title,
                type = m.Type.ToString(),
                family = m.Family.ToString(),
                format = m.Format.ToString(),
                fileSize = m.FileSize,
                previewImageUrl = m.PreviewImageUrl,
                providerUrl = m.ProviderUrl,
                description = m.Description,
                tags = m.Tags,
                variants = m.Variants.Select(v => new { v.FileName, v.FileSize, format = v.Format.ToString(), v.Quantization })
            }),
            totalCount = result.TotalCount,
            hasMore = result.HasMore,
            nextCursor = result.NextCursor
        });
    }

    [McpServerTool(Name = "list_local_models"), Description(
        "List models installed on the local machine. Filter by type, family, or search term.")]
    public static async Task<string> ListLocalModels(
        IModelCatalogService catalogService,
        [Description("Search term to filter by name")] string? searchTerm = null,
        [Description("Model type: Checkpoint, LoRA, VAE, Embedding, ControlNet")] string? type = null,
        [Description("Model family: SD15, SDXL, Flux, Pony, Illustrious")] string? family = null,
        [Description("Max results")] int limit = 50)
    {
        var typeFilter = type is not null && Enum.TryParse<ModelType>(type, true, out var t) ? t : (ModelType?)null;
        var familyFilter = family is not null && Enum.TryParse<ModelFamily>(family, true, out var f) ? f : (ModelFamily?)null;

        var models = await catalogService.ListAsync(new ModelFilter(searchTerm, familyFilter, Type: typeFilter, Take: limit));

        return JsonSerializer.Serialize(new
        {
            models = models.Select(m => new
            {
                id = m.Id,
                title = m.Title,
                type = m.Type.ToString(),
                family = m.ModelFamily.ToString(),
                format = m.Format.ToString(),
                fileSize = m.FileSize,
                filePath = m.FilePath,
                status = m.Status.ToString(),
                tags = m.Tags,
                description = m.Description,
                civitAIUrl = m.CompatibilityHints // placeholder — will be enriched
            }),
            count = models.Count
        });
    }

    [McpServerTool(Name = "download_model"), Description(
        "Download a model from CivitAI or HuggingFace. Returns a job ID to track progress.")]
    public static async Task<string> DownloadModel(
        IModelCatalogService catalogService,
        [Description("Provider: 'civitai' or 'huggingface'")] string provider,
        [Description("External model ID (from search_models results)")] string externalId,
        [Description("Specific file variant to download (filename from variants list)")] string? variantFileName = null)
    {
        var defaultRoot = new StorageRoot(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "StableDiffusionStudio", "Downloads"),
            "MCP Downloads");

        var request = new DownloadRequest(provider, externalId, variantFileName, defaultRoot, ModelType.Checkpoint);
        var jobId = await catalogService.RequestDownloadAsync(request);

        return JsonSerializer.Serialize(new
        {
            jobId,
            message = "Download started. Check the Jobs page for progress.",
            provider,
            externalId
        });
    }

    [McpServerTool(Name = "get_model_details"), Description(
        "Get full details for a local model by ID.")]
    public static async Task<string> GetModelDetails(
        IModelCatalogService catalogService,
        [Description("Model GUID from list_local_models")] string modelId)
    {
        if (!Guid.TryParse(modelId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid model ID" });

        var model = await catalogService.GetByIdAsync(id);
        if (model is null)
            return JsonSerializer.Serialize(new { error = "Model not found" });

        return JsonSerializer.Serialize(new
        {
            id = model.Id,
            title = model.Title,
            type = model.Type.ToString(),
            family = model.ModelFamily.ToString(),
            format = model.Format.ToString(),
            fileSize = model.FileSize,
            filePath = model.FilePath,
            status = model.Status.ToString(),
            source = model.Source,
            tags = model.Tags,
            description = model.Description,
            previewImagePath = model.PreviewImagePath,
            detectedAt = model.DetectedAt
        });
    }

    [McpServerTool(Name = "scan_models"), Description(
        "Scan storage roots for new models. Discovers models on disk and adds them to the catalog.")]
    public static async Task<string> ScanModels(
        IModelCatalogService catalogService)
    {
        var result = await catalogService.ScanAsync(new ScanModelsCommand(null));
        return JsonSerializer.Serialize(new
        {
            newModels = result.NewCount,
            updatedModels = result.UpdatedCount,
            missingModels = result.MissingCount
        });
    }
}
