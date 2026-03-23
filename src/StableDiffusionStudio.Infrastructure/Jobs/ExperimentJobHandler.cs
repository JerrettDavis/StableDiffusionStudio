using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Infrastructure.Jobs;

public class ExperimentJobHandler : IJobHandler
{
    private readonly IExperimentRepository _experimentRepository;
    private readonly IModelCatalogRepository _modelCatalogRepository;
    private readonly IInferenceBackend _inferenceBackend;
    private readonly IAppPaths _appPaths;
    private readonly IContentSafetyService _contentSafetyService;
    private readonly IExperimentNotifier _experimentNotifier;
    private readonly IFluxComponentResolver? _fluxResolver;
    private readonly ILogger<ExperimentJobHandler> _logger;

    public ExperimentJobHandler(
        IExperimentRepository experimentRepository,
        IModelCatalogRepository modelCatalogRepository,
        IInferenceBackend inferenceBackend,
        IAppPaths appPaths,
        IContentSafetyService contentSafetyService,
        IExperimentNotifier experimentNotifier,
        ILogger<ExperimentJobHandler> logger,
        IFluxComponentResolver? fluxResolver = null)
    {
        _experimentRepository = experimentRepository;
        _modelCatalogRepository = modelCatalogRepository;
        _inferenceBackend = inferenceBackend;
        _appPaths = appPaths;
        _contentSafetyService = contentSafetyService;
        _experimentNotifier = experimentNotifier;
        _fluxResolver = fluxResolver;
        _logger = logger;
    }

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        // ── 1. Deserialize job data ───────────────────────────────────────────────
        ExperimentJobData? jobData;
        try
        {
            jobData = JsonSerializer.Deserialize<ExperimentJobData>(job.Data!);
        }
        catch (JsonException)
        {
            jobData = null;
        }

        if (jobData is null || jobData.RunId == Guid.Empty)
        {
            job.Fail("Invalid experiment job data");
            return;
        }

        // ── 2. Load run and experiment ────────────────────────────────────────────
        var run = await _experimentRepository.GetRunByIdAsync(jobData.RunId, ct);
        if (run is null)
        {
            job.Fail($"Experiment run {jobData.RunId} not found");
            return;
        }

        var experiment = await _experimentRepository.GetByIdAsync(run.ExperimentId, ct);
        if (experiment is null)
        {
            job.Fail($"Experiment {run.ExperimentId} not found");
            return;
        }

        // ── 3. Start run ──────────────────────────────────────────────────────────
        run.Start();
        await _experimentRepository.UpdateRunAsync(run, ct);
        job.UpdateProgress(2, "Starting experiment run");

        var experimentId = experiment.Id.ToString();
        var runId = run.Id.ToString();

        try
        {
            // ── 4. Expand sweep combinations ──────────────────────────────────────
            var baseParams = experiment.GetBaseParameters();
            var axes = experiment.GetSweepAxes();
            var axis1 = axes.Count >= 1 ? axes[0] : null;
            var axis2 = axes.Count >= 2 ? axes[1] : null;
            var combinations = SweepExpander.Expand(baseParams, axis1, axis2);

            _logger.LogInformation(
                "Experiment run {RunId}: {Count} combinations across {Axes} axis/axes",
                run.Id, combinations.Count, axes.Count);

            // ── 5. Output directory ───────────────────────────────────────────────
            var outputDir = Path.Combine(_appPaths.AssetsDirectory, "experiments", experimentId, runId);
            Directory.CreateDirectory(outputDir);

            // ── 6. Group combinations by checkpoint for efficient model loading ───
            var groups = combinations
                .GroupBy(c => c.Parameters.CheckpointModelId)
                .ToList();

            Guid? lastLoadedCheckpointId = null;
            int completedSoFar = 0;

            foreach (var group in groups)
            {
                ct.ThrowIfCancellationRequested();

                var checkpointId = group.Key;

                // ── 7a. Load model if checkpoint changed ──────────────────────────
                if (lastLoadedCheckpointId != checkpointId)
                {
                    job.UpdateProgress(
                        ProgressPercent(completedSoFar, combinations.Count, phase: "loading"),
                        "Loading model");

                    var checkpoint = await _modelCatalogRepository.GetByIdAsync(checkpointId, ct);
                    if (checkpoint is null)
                    {
                        _logger.LogWarning(
                            "Checkpoint {CheckpointId} not found — skipping {Count} combination(s)",
                            checkpointId, group.Count());

                        completedSoFar += group.Count();
                        continue;
                    }

                    // Resolve VAE
                    string? vaePath = null;
                    var firstParams = group.First().Parameters;
                    if (firstParams.VaeModelId.HasValue)
                    {
                        var vae = await _modelCatalogRepository.GetByIdAsync(firstParams.VaeModelId.Value, ct);
                        vaePath = vae?.FilePath;
                    }

                    // Resolve LoRA paths (from first combo — LoRA set should be consistent across the group)
                    var loras = new List<LoraLoadInfo>();
                    foreach (var loraRef in firstParams.Loras)
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
                            if (string.IsNullOrWhiteSpace(vaePath) && components.VaePath != null)
                                vaePath = components.VaePath;
                        }
                    }

                    _logger.LogInformation(
                        "Loading model for experiment group: {FileName}",
                        Path.GetFileName(checkpoint.FilePath));

                    await _inferenceBackend.LoadModelAsync(
                        new ModelLoadRequest(checkpoint.FilePath, vaePath, loras, clipLPath, t5xxlPath), ct);

                    lastLoadedCheckpointId = checkpointId;
                }

                // ── 7b. Generate each combination in the group ────────────────────
                foreach (var combo in group)
                {
                    ct.ThrowIfCancellationRequested();

                    var sw = Stopwatch.StartNew();

                    // Determine seed
                    var seed = run.UseFixedSeed
                        ? run.FixedSeed
                        : Random.Shared.NextInt64();

                    // Load init image bytes if experiment has an init image path
                    byte[]? initImageBytes = null;
                    if (!string.IsNullOrWhiteSpace(experiment.InitImagePath)
                        && File.Exists(experiment.InitImagePath))
                    {
                        initImageBytes = await File.ReadAllBytesAsync(experiment.InitImagePath, ct);
                    }

                    var p = combo.Parameters;
                    var request = new InferenceRequest(
                        PositivePrompt: p.PositivePrompt,
                        NegativePrompt: p.NegativePrompt,
                        Sampler: p.Sampler,
                        Scheduler: p.Scheduler,
                        Steps: p.Steps,
                        CfgScale: p.CfgScale,
                        Seed: seed,
                        Width: p.Width,
                        Height: p.Height,
                        BatchSize: 1,
                        ClipSkip: p.ClipSkip,
                        Eta: p.Eta,
                        InitImage: initImageBytes,
                        DenoisingStrength: p.DenoisingStrength);

                    var gridX = combo.GridX;
                    var gridY = combo.GridY;
                    var stepPreview = new DirectProgress<InferenceProgress>(p =>
                    {
                        if (p.PreviewImageBytes is not null)
                        {
                            try
                            {
                                _experimentNotifier.SendStepPreviewAsync(
                                    runId, gridX, gridY,
                                    $"Step {p.Step}/{p.TotalSteps}",
                                    p.PreviewImageBytes).GetAwaiter().GetResult();
                            }
                            catch { /* Non-critical preview delivery */ }
                        }
                    });
                    var result = await _inferenceBackend.GenerateAsync(request, stepPreview, ct);

                    sw.Stop();

                    if (!result.Success || result.Images.Count == 0)
                    {
                        _logger.LogWarning(
                            "Combination ({GridX},{GridY}) failed for run {RunId}: {Error}",
                            combo.GridX, combo.GridY, run.Id, result.Error ?? "no images returned");

                        completedSoFar++;
                        run.IncrementCompleted();
                        await _experimentRepository.UpdateRunAsync(run, ct);
                        continue;
                    }

                    var imageData = result.Images[0];

                    // ── Save image to disk ────────────────────────────────────────
                    var fileName = $"grid_{combo.GridX}_{combo.GridY}_{imageData.Seed}.png";
                    var filePath = Path.Combine(outputDir, fileName);
                    await File.WriteAllBytesAsync(filePath, imageData.ImageBytes, ct);

                    // ── Build axis values JSON ─────────────────────────────────────
                    var axisValuesJson = JsonSerializer.Serialize(combo.AxisValues);

                    // ── Create domain image entity ────────────────────────────────
                    var runImage = ExperimentRunImage.Create(
                        runId: run.Id,
                        filePath: filePath,
                        seed: imageData.Seed,
                        generationTimeSeconds: sw.Elapsed.TotalSeconds,
                        axisValuesJson: axisValuesJson,
                        gridX: combo.GridX,
                        gridY: combo.GridY);

                    // ── Classify content safety (non-critical) ────────────────────
                    try
                    {
                        var classification = await _contentSafetyService.ClassifyAsync(imageData.ImageBytes, ct);
                        runImage.SetContentRating(classification.Rating, classification.NsfwScore);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Content safety classification failed for grid ({GridX},{GridY}) — continuing",
                            combo.GridX, combo.GridY);
                    }

                    // ── Persist image, update progress ────────────────────────────
                    run.AddImage(runImage);
                    run.IncrementCompleted();
                    await _experimentRepository.UpdateRunAsync(run, ct);

                    completedSoFar++;
                    var pct = ProgressPercent(completedSoFar, combinations.Count, phase: "generating");
                    job.UpdateProgress(pct, $"Generated {completedSoFar}/{combinations.Count}");

                    // ── Notify UI of progress (non-critical) ──────────────────────
                    var imageUrl = _appPaths.GetImageUrl(filePath);
                    try
                    {
                        await _experimentNotifier.SendProgressAsync(
                            runId,
                            completedIndex: completedSoFar,
                            totalCount: combinations.Count,
                            axisValuesJson: axisValuesJson,
                            imageUrl: imageUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to send experiment progress notification");
                    }
                }
            }

            // ── 8. Complete the run ───────────────────────────────────────────────
            run.Complete();
            await _experimentRepository.UpdateRunAsync(run, ct);
            job.UpdateProgress(100, "Complete");

            _logger.LogInformation(
                "Experiment run {RunId} completed: {Completed}/{Total} images",
                run.Id, run.CompletedCount, run.TotalCombinations);

            try
            {
                await _experimentNotifier.SendCompletedAsync(runId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send experiment completed notification");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ── 9. Fail the run ───────────────────────────────────────────────────
            _logger.LogError(ex, "Experiment run {RunId} failed", run.Id);
            run.Fail(ex.Message);
            await _experimentRepository.UpdateRunAsync(run, ct);
            job.Fail(ex.Message);

            try
            {
                await _experimentNotifier.SendFailedAsync(runId, ex.Message);
            }
            catch (Exception notifyEx)
            {
                _logger.LogDebug(notifyEx, "Failed to send experiment failed notification");
            }
        }
    }

    /// <summary>
    /// Maps completed-combination count to a job progress percentage.
    /// Reserves 0–4% for startup and 95–100% for finalisation.
    /// </summary>
    private static int ProgressPercent(int completed, int total, string phase)
    {
        if (phase == "loading") return Math.Max(2, completed == 0 ? 5 : ProgressPercent(completed, total, "generating") - 5);
        if (total <= 0) return 95;
        var raw = (double)completed / total;
        return 5 + (int)(raw * 90.0); // 5–95 %
    }

    /// <summary>
    /// Mirrors the internal ExperimentJobData record produced by ExperimentService.
    /// Defined here to avoid a cross-assembly visibility dependency on an internal type.
    /// </summary>
    private sealed record ExperimentJobData(Guid RunId);
}
