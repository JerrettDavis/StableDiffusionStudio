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
public class GenerationTools
{
    [McpServerTool(Name = "generate_image"), Description(
        "Generate an image using Stable Diffusion. Supports txt2img (no initImage) and img2img (with initImage). " +
        "Returns a job ID that can be polled with get_generation_status. The image will be saved to disk.")]
    public static async Task<string> GenerateImage(
        IGenerationService generationService,
        IModelCatalogService catalogService,
        [Description("The positive prompt describing what to generate")] string prompt,
        [Description("Negative prompt describing what to avoid")] string negativePrompt = "",
        [Description("GUID of the checkpoint model to use. Use list_local_models to find IDs.")] string checkpointModelId = "",
        [Description("Sampler: EulerA, Euler, DPMPlusPlus2M, DPMPlusPlus2S, DDIM, LCM")] string sampler = "EulerA",
        [Description("Number of inference steps (1-150)")] int steps = 20,
        [Description("CFG scale (1.0-30.0)")] double cfgScale = 7.0,
        [Description("Image width in pixels (multiple of 64)")] int width = 512,
        [Description("Image height in pixels (multiple of 64)")] int height = 512,
        [Description("Seed (-1 for random)")] long seed = -1,
        [Description("Denoising strength for img2img (0.0-1.0)")] double denoisingStrength = 1.0,
        [Description("Clip skip layers (1-12)")] int clipSkip = 1,
        [Description("Number of images per batch")] int batchSize = 1,
        [Description("Base64-encoded init image for img2img mode")] string? initImageBase64 = null)
    {
        if (string.IsNullOrWhiteSpace(checkpointModelId))
        {
            var models = await catalogService.ListAsync(new ModelFilter(Type: ModelType.Checkpoint, Take: 1));
            if (models.Count == 0) return JsonSerializer.Serialize(new { error = "No checkpoint models available. Scan models first." });
            checkpointModelId = models[0].Id.ToString();
        }

        if (!Guid.TryParse(checkpointModelId, out var modelId))
            return JsonSerializer.Serialize(new { error = $"Invalid checkpoint model ID: {checkpointModelId}" });

        var parsedSampler = Enum.TryParse<Sampler>(sampler, true, out var s) ? s : Sampler.EulerA;
        var mode = initImageBase64 is not null ? GenerationMode.ImageToImage : GenerationMode.TextToImage;

        var parameters = new GenerationParameters
        {
            PositivePrompt = prompt,
            NegativePrompt = negativePrompt,
            CheckpointModelId = modelId,
            Sampler = parsedSampler,
            Steps = Math.Clamp(steps, 1, 150),
            CfgScale = Math.Clamp(cfgScale, 1.0, 30.0),
            Width = Math.Max(64, (width / 64) * 64),
            Height = Math.Max(64, (height / 64) * 64),
            Seed = seed,
            BatchSize = Math.Clamp(batchSize, 1, 8),
            ClipSkip = Math.Clamp(clipSkip, 1, 12),
            DenoisingStrength = Math.Clamp(denoisingStrength, 0.0, 1.0),
            Mode = mode,
        };

        byte[]? initBytes = null;
        if (initImageBase64 is not null)
        {
            try { initBytes = Convert.FromBase64String(initImageBase64); }
            catch { return JsonSerializer.Serialize(new { error = "Invalid base64 for initImage" }); }
        }

        // Use a default project for MCP-generated images
        var projectId = Guid.Empty;
        var command = new CreateGenerationCommand(projectId, parameters, initBytes);
        var jobs = await generationService.CreateWithMatrixAsync(command);

        return JsonSerializer.Serialize(new
        {
            jobId = jobs[0].Id,
            status = jobs[0].Status.ToString(),
            message = $"Generation started. Poll with get_generation_status(jobId: \"{jobs[0].Id}\")"
        });
    }

    [McpServerTool(Name = "get_generation_status"), Description(
        "Check the status of a generation job. Returns progress, status, and image data when complete.")]
    public static async Task<string> GetGenerationStatus(
        IGenerationService generationService,
        IAppPaths appPaths,
        [Description("The job ID returned by generate_image")] string jobId)
    {
        if (!Guid.TryParse(jobId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid job ID" });

        var job = await generationService.GetJobAsync(id);
        if (job is null)
            return JsonSerializer.Serialize(new { error = "Job not found" });

        var result = new
        {
            jobId = job.Id,
            status = job.Status.ToString(),
            prompt = job.Parameters.PositivePrompt,
            imageCount = job.Images.Count,
            images = job.Images.Select(img => new
            {
                id = img.Id,
                seed = img.Seed,
                width = img.Width,
                height = img.Height,
                filePath = img.FilePath,
                url = appPaths.GetImageUrl(img.FilePath),
                generationTime = img.GenerationTimeSeconds
            })
        };

        return JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "list_recent_generations"), Description(
        "List recent generation jobs with their images and parameters.")]
    public static async Task<string> ListRecentGenerations(
        IGenerationService generationService,
        [Description("Maximum number of jobs to return")] int limit = 10)
    {
        var jobs = await generationService.ListJobsForProjectAsync(Guid.Empty);
        var recent = jobs.Take(Math.Min(limit, 50)).Select(j => new
        {
            jobId = j.Id,
            status = j.Status.ToString(),
            prompt = j.Parameters.PositivePrompt,
            model = j.Parameters.CheckpointModelId,
            imageCount = j.Images.Count,
            createdAt = j.CreatedAt
        });

        return JsonSerializer.Serialize(new { jobs = recent });
    }
}
