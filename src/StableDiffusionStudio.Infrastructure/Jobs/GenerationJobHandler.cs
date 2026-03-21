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
    private readonly IGenerationNotifier _generationNotifier;
    private readonly IFluxComponentResolver? _fluxResolver;
    private readonly IOutputSettingsProvider? _outputSettingsProvider;
    private readonly ILogger<GenerationJobHandler> _logger;

    public GenerationJobHandler(
        IGenerationJobRepository generationJobRepository,
        IModelCatalogRepository modelCatalogRepository,
        IInferenceBackend inferenceBackend,
        IAppPaths appPaths,
        IContentSafetyService contentSafetyService,
        IGenerationNotifier generationNotifier,
        ILogger<GenerationJobHandler> logger,
        IFluxComponentResolver? fluxResolver = null,
        IOutputSettingsProvider? outputSettingsProvider = null)
    {
        _generationJobRepository = generationJobRepository;
        _modelCatalogRepository = modelCatalogRepository;
        _inferenceBackend = inferenceBackend;
        _appPaths = appPaths;
        _contentSafetyService = contentSafetyService;
        _generationNotifier = generationNotifier;
        _fluxResolver = fluxResolver;
        _outputSettingsProvider = outputSettingsProvider;
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

            // Resolve Flux components if needed
            string? clipLPath = null;
            string? t5xxlPath = null;
            var checkpointName = Path.GetFileName(checkpoint.FilePath).ToLowerInvariant();
            if (checkpointName.Contains("flux") && _fluxResolver != null)
            {
                var components = await _fluxResolver.ResolveAsync(checkpoint.FilePath, ct);
                if (components != null)
                {
                    clipLPath = components.ClipLPath;
                    t5xxlPath = components.T5xxlPath;
                    // Use Flux VAE if no VAE was explicitly selected
                    if (string.IsNullOrWhiteSpace(vaePath) && components.VaePath != null)
                        vaePath = components.VaePath;
                }
            }

            // Load model
            await _inferenceBackend.LoadModelAsync(
                new ModelLoadRequest(checkpoint.FilePath, vaePath, loras, clipLPath, t5xxlPath), ct);
            job.UpdateProgress(20, "Generating");

            // Generate — run BatchCount iterations, each producing BatchSize images
            var parameters = generationJob.Parameters;
            var batchCount = Math.Max(1, parameters.BatchCount);
            var allImages = new List<GeneratedImageData>();

            // Load init image bytes for img2img / inpainting if applicable
            byte[]? initImageBytes = null;
            if (parameters.Mode is Domain.Enums.GenerationMode.ImageToImage or Domain.Enums.GenerationMode.Inpainting
                && !string.IsNullOrWhiteSpace(parameters.InitImagePath)
                && File.Exists(parameters.InitImagePath))
            {
                initImageBytes = await File.ReadAllBytesAsync(parameters.InitImagePath, ct);
                _logger.LogInformation("Loaded init image for {Mode}: {Path} ({Size} bytes)",
                    parameters.Mode, parameters.InitImagePath, initImageBytes.Length);
            }

            // Load mask image bytes for inpainting if applicable
            byte[]? maskImageBytes = null;
            if (parameters.Mode == Domain.Enums.GenerationMode.Inpainting
                && !string.IsNullOrWhiteSpace(parameters.MaskImagePath)
                && File.Exists(parameters.MaskImagePath))
            {
                maskImageBytes = await File.ReadAllBytesAsync(parameters.MaskImagePath, ct);
                _logger.LogInformation("Loaded mask image for inpainting: {Path} ({Size} bytes)",
                    parameters.MaskImagePath, maskImageBytes.Length);
            }

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
                    parameters.ClipSkip,
                    parameters.Eta,
                    initImageBytes,
                    parameters.DenoisingStrength,
                    maskImageBytes
                );

                var projectIdStr = generationJob.ProjectId.ToString();

                // Send an immediate "step 0" event so the UI skeleton card appears right away.
                // Some models/samplers (e.g. turbo models with DPM++ SDE) complete very fast
                // or don't fire native progress callbacks reliably — this ensures the UI
                // always shows a progress indicator as soon as generation begins.
                try
                {
                    await _generationNotifier.SendPreviewAsync(
                        projectIdStr, 0, request.Steps, "");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send initial progress notification");
                }

                // Use a direct IProgress implementation that sends synchronously
                // Progress<T> with async callbacks doesn't work reliably without a SynchronizationContext
                var progress = new DirectProgress<InferenceProgress>(p =>
                {
                    var batchProgress = (double)batchIndex / batchCount;
                    var stepProgress = (double)p.Step / p.TotalSteps / batchCount;
                    var pct = 20 + (int)((batchProgress + stepProgress) * 60.0);
                    job.UpdateProgress(pct, $"Batch {batchIndex + 1}/{batchCount}: {p.Phase}");

                    // Always send step progress via SignalR (even without preview image)
                    try
                    {
                        string? previewDataUrl = null;
                        if (p.PreviewImageBytes is not null)
                        {
                            previewDataUrl = $"data:image/png;base64,{Convert.ToBase64String(p.PreviewImageBytes)}";
                        }
                        _generationNotifier.SendPreviewAsync(
                            projectIdStr, p.Step, p.TotalSteps,
                            previewDataUrl ?? "")
                            .ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                    _logger.LogDebug(t.Exception, "Failed to send progress");
                            }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to send progress notification");
                    }
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

            // Hires Fix — two-pass upscaling
            if (parameters.HiresFixEnabled && allImages.Count > 0)
            {
                job.UpdateProgress(70, "Hires fix — upscaling");
                var hiresImages = new List<GeneratedImageData>();

                foreach (var img in allImages)
                {
                    ct.ThrowIfCancellationRequested();

                    var hiresRequest = new InferenceRequest(
                        parameters.PositivePrompt,
                        parameters.NegativePrompt,
                        parameters.Sampler,
                        parameters.Scheduler,
                        parameters.HiresSteps > 0 ? parameters.HiresSteps : parameters.Steps,
                        parameters.CfgScale,
                        img.Seed,
                        (int)(parameters.Width * parameters.HiresUpscaleFactor),
                        (int)(parameters.Height * parameters.HiresUpscaleFactor),
                        1, // batch size 1 per hires pass
                        parameters.ClipSkip,
                        parameters.Eta,
                        img.ImageBytes, // use first-pass as init image
                        parameters.HiresDenoisingStrength
                    );

                    var hiresResult = await _inferenceBackend.GenerateAsync(hiresRequest,
                        new DirectProgress<InferenceProgress>(_ => { }), ct);

                    if (hiresResult.Success && hiresResult.Images.Count > 0)
                        hiresImages.Add(hiresResult.Images[0]);
                    else
                        hiresImages.Add(img); // Keep original if hires fails
                }

                allImages = hiresImages;
            }

            job.UpdateProgress(85, "Saving images");

            // Save images to disk
            // Free memory after generation before saving/classification
            GC.Collect(2, GCCollectionMode.Forced, false);

            // Load output settings for filename formatting
            Domain.ValueObjects.OutputSettings? outputSettings = null;
            if (_outputSettingsProvider is not null)
            {
                try { outputSettings = await _outputSettingsProvider.GetSettingsAsync(ct); }
                catch { /* Use default if settings unavailable */ }
            }
            outputSettings ??= Domain.ValueObjects.OutputSettings.Default;

            var assetsDir = !string.IsNullOrWhiteSpace(outputSettings.CustomOutputDirectory)
                ? Path.Combine(outputSettings.CustomOutputDirectory, generationJob.ProjectId.ToString(), generationJob.Id.ToString())
                : _appPaths.GetJobAssetsDirectory(generationJob.ProjectId, generationJob.Id);
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
                var fileName = outputSettings.FormatFilename(
                    imageData.Seed, parameters.PositivePrompt,
                    DateTimeOffset.UtcNow, modelName ?? "unknown");
                var filePath = Path.Combine(assetsDir, fileName);

                // Embed A1111-compatible metadata into PNG before saving
                byte[] bytesToSave = imageData.ImageBytes;
                // Classify content safety (always runs regardless of filter mode)
                var classification = await _contentSafetyService.ClassifyAsync(imageData.ImageBytes, ct);

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

                    if (classification.Rating != ContentRating.Unknown)
                        a1111Params += $", Content Rating: {classification.Rating} ({classification.NsfwScore:P0})";

                    bytesToSave = PngMetadataService.EmbedMetadata(imageData.ImageBytes, a1111Params);
                }
                catch (Exception ex)
                {
                    // If embedding fails, save original bytes — never break the output
                    _logger.LogWarning(ex, "Failed to embed PNG metadata for seed {Seed}", imageData.Seed);
                }

                await File.WriteAllBytesAsync(filePath, bytesToSave, ct);

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

            await _generationNotifier.SendCompletedAsync(generationJob.ProjectId.ToString());

            _logger.LogInformation("Generation job {JobId} completed with {ImageCount} images ({BatchCount} batches)",
                generationJob.Id, allImages.Count, batchCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Generation job {JobId} failed", generationJob.Id);
            generationJob.Fail(ex.Message);
            await _generationJobRepository.UpdateAsync(generationJob, ct);
            job.Fail(ex.Message);

            await _generationNotifier.SendFailedAsync(generationJob.ProjectId.ToString(), ex.Message);
            // Don't re-throw — we've handled the error by updating both job records
        }
    }

    private sealed record GenerationJobData(Guid GenerationJobId);
}

/// <summary>
/// An IProgress implementation that invokes the callback synchronously on the reporting thread.
/// Unlike Progress<T>, this doesn't capture SynchronizationContext and doesn't use ThreadPool.
/// </summary>
internal class DirectProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;
    public DirectProgress(Action<T> handler) => _handler = handler;
    public void Report(T value) => _handler(value);
}
