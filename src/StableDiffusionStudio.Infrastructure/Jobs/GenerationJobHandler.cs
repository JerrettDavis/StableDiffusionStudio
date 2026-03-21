using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Services;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class GenerationJobHandler : IJobHandler
{
    private readonly IGenerationJobRepository _generationJobRepository;
    private readonly IModelCatalogRepository _modelCatalogRepository;
    private readonly IInferenceBackend _inferenceBackend;
    private readonly IAppPaths _appPaths;
    private readonly IContentSafetyService _contentSafetyService;
    private readonly ILogger<GenerationJobHandler> _logger;

    public GenerationJobHandler(
        IGenerationJobRepository generationJobRepository,
        IModelCatalogRepository modelCatalogRepository,
        IInferenceBackend inferenceBackend,
        IAppPaths appPaths,
        IContentSafetyService contentSafetyService,
        ILogger<GenerationJobHandler> logger)
    {
        _generationJobRepository = generationJobRepository;
        _modelCatalogRepository = modelCatalogRepository;
        _inferenceBackend = inferenceBackend;
        _appPaths = appPaths;
        _contentSafetyService = contentSafetyService;
        _logger = logger;
    }

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        GenerationJobData? jobData;
        try
        {
            jobData = JsonSerializer.Deserialize<GenerationJobData>(job.Data!);
        }
        catch (JsonException)
        {
            jobData = null;
        }

        if (jobData is null || jobData.GenerationJobId == Guid.Empty)
        {
            job.Fail("Invalid generation job data");
            return;
        }

        var generationJob = await _generationJobRepository.GetByIdAsync(jobData.GenerationJobId, ct);
        if (generationJob is null)
        {
            job.Fail($"Generation job {jobData.GenerationJobId} not found");
            return;
        }

        generationJob.Start();
        await _generationJobRepository.UpdateAsync(generationJob, ct);
        job.UpdateProgress(5, "Loading model");

        try
        {
            // Resolve checkpoint path
            var checkpoint = await _modelCatalogRepository.GetByIdAsync(generationJob.Parameters.CheckpointModelId, ct);
            if (checkpoint is null)
            {
                generationJob.Fail($"Checkpoint model {generationJob.Parameters.CheckpointModelId} not found");
                await _generationJobRepository.UpdateAsync(generationJob, ct);
                job.Fail("Checkpoint model not found");
                return;
            }

            // Resolve VAE path if specified
            string? vaePath = null;
            if (generationJob.Parameters.VaeModelId.HasValue)
            {
                var vae = await _modelCatalogRepository.GetByIdAsync(generationJob.Parameters.VaeModelId.Value, ct);
                vaePath = vae?.FilePath;
            }

            // Resolve LoRA paths
            var loras = new List<LoraLoadInfo>();
            foreach (var loraRef in generationJob.Parameters.Loras)
            {
                var loraModel = await _modelCatalogRepository.GetByIdAsync(loraRef.ModelId, ct);
                if (loraModel is not null)
                    loras.Add(new LoraLoadInfo(loraModel.FilePath, loraRef.Weight));
            }

            // Load model
            await _inferenceBackend.LoadModelAsync(new ModelLoadRequest(checkpoint.FilePath, vaePath, loras), ct);
            job.UpdateProgress(20, "Generating");

            // Generate — run BatchCount iterations, each producing BatchSize images
            var parameters = generationJob.Parameters;
            var batchCount = Math.Max(1, parameters.BatchCount);
            var allImages = new List<GeneratedImageData>();

            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                ct.ThrowIfCancellationRequested();

                // Offset the seed for each batch run so we don't produce duplicates
                var batchSeed = parameters.Seed == -1 ? -1 : parameters.Seed + (batchIndex * parameters.BatchSize);

                var request = new InferenceRequest(
                    parameters.PositivePrompt,
                    parameters.NegativePrompt,
                    parameters.Sampler,
                    parameters.Scheduler,
                    parameters.Steps,
                    parameters.CfgScale,
                    batchSeed,
                    parameters.Width,
                    parameters.Height,
                    parameters.BatchSize,
                    parameters.ClipSkip
                );

                var progress = new Progress<InferenceProgress>(p =>
                {
                    var batchProgress = (double)batchIndex / batchCount;
                    var stepProgress = (double)p.Step / p.TotalSteps / batchCount;
                    var pct = 20 + (int)((batchProgress + stepProgress) * 60.0);
                    job.UpdateProgress(pct, $"Batch {batchIndex + 1}/{batchCount}: {p.Phase}");
                });

                var result = await _inferenceBackend.GenerateAsync(request, progress, ct);

                if (!result.Success)
                {
                    generationJob.Fail(result.Error ?? "Generation failed");
                    await _generationJobRepository.UpdateAsync(generationJob, ct);
                    job.Fail(result.Error ?? "Generation failed");
                    return;
                }

                allImages.AddRange(result.Images);
            }

            job.UpdateProgress(85, "Saving images");

            // Save images to disk
            var assetsDir = _appPaths.GetJobAssetsDirectory(generationJob.ProjectId, generationJob.Id);
            Directory.CreateDirectory(assetsDir);

            var parametersJson = JsonSerializer.Serialize(parameters);

            // Resolve model name for metadata embedding
            string? modelName = null;
            try
            {
                var checkpointModel = await _modelCatalogRepository.GetByIdAsync(parameters.CheckpointModelId, ct);
                modelName = checkpointModel?.Title;
            }
            catch { /* Non-critical — proceed without model name */ }

            var filterMode = await _contentSafetyService.GetFilterModeAsync(ct);

            foreach (var imageData in allImages)
            {
                var fileName = $"{imageData.Seed}.png";
                var filePath = Path.Combine(assetsDir, fileName);

                // Embed A1111-compatible metadata into PNG before saving
                byte[] bytesToSave = imageData.ImageBytes;
                try
                {
                    var a1111Params = PngMetadataService.FormatA1111Parameters(
                        parameters.PositivePrompt,
                        parameters.NegativePrompt,
                        parameters.Steps,
                        parameters.Sampler.ToString(),
                        parameters.CfgScale,
                        imageData.Seed,
                        parameters.Width,
                        parameters.Height,
                        modelName,
                        parameters.ClipSkip);
                    bytesToSave = PngMetadataService.EmbedMetadata(imageData.ImageBytes, a1111Params);
                }
                catch (Exception ex)
                {
                    // If embedding fails, save original bytes — never break the output
                    _logger.LogWarning(ex, "Failed to embed PNG metadata for seed {Seed}", imageData.Seed);
                }

                await File.WriteAllBytesAsync(filePath, bytesToSave, ct);

                // Classify content safety
                var classification = await _contentSafetyService.ClassifyAsync(imageData.ImageBytes, ct);

                var generatedImage = GeneratedImage.Create(
                    generationJob.Id,
                    filePath,
                    imageData.Seed,
                    parameters.Width,
                    parameters.Height,
                    imageData.GenerationTimeSeconds,
                    parametersJson);

                generatedImage.SetContentRating(classification.Rating, classification.NsfwScore);

                // If BlockAndDelete mode and NSFW detected, delete the file and skip
                if (filterMode == NsfwFilterMode.BlockAndDelete && classification.Rating == ContentRating.Nsfw)
                {
                    try { File.Delete(filePath); } catch { /* Best effort */ }
                    _logger.LogWarning("NSFW content detected and deleted: {Seed}", imageData.Seed);
                    continue;
                }

                generationJob.AddImage(generatedImage);
            }

            // Model kept loaded for reuse by subsequent generations

            generationJob.Complete();
            await _generationJobRepository.UpdateAsync(generationJob, ct);
            job.UpdateProgress(100, "Complete");

            _logger.LogInformation("Generation job {JobId} completed with {ImageCount} images ({BatchCount} batches)",
                generationJob.Id, allImages.Count, batchCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Generation job {JobId} failed", generationJob.Id);
            generationJob.Fail(ex.Message);
            await _generationJobRepository.UpdateAsync(generationJob, ct);
            job.Fail(ex.Message);
            // Don't re-throw — we've handled the error by updating both job records
        }
    }

    private sealed record GenerationJobData(Guid GenerationJobId);
}
