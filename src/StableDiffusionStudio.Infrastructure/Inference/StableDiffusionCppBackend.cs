using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using StableDiffusion.NET;
using HPPH.System.Drawing;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using Sampler = StableDiffusionStudio.Domain.Enums.Sampler;
using Scheduler = StableDiffusionStudio.Domain.Enums.Scheduler;
using SdSampler = StableDiffusion.NET.Sampler;
using SdScheduler = StableDiffusion.NET.Scheduler;

namespace StableDiffusionStudio.Infrastructure.Inference;

public class StableDiffusionCppBackend : IInferenceBackend, IDisposable
{
    private readonly ILogger<StableDiffusionCppBackend> _logger;
    private DiffusionModel? _model;
    private bool _nativeAvailable;
    private bool _checkedAvailability;

    public string BackendId => "stable-diffusion-cpp";
    public string DisplayName => "Stable Diffusion (C++)";
    public InferenceCapabilities Capabilities => new(
        SupportedFamilies: [ModelFamily.SD15, ModelFamily.SDXL, ModelFamily.Flux],
        SupportedSamplers: [Sampler.Euler, Sampler.EulerA, Sampler.DPMPlusPlus2M, Sampler.DPMPlusPlusSDE,
                           Sampler.DDIM, Sampler.Heun, Sampler.DPM2, Sampler.LMS, Sampler.UniPC],
        MaxWidth: 2048, MaxHeight: 2048,
        SupportsLoRA: true, SupportsVAE: true);

    public StableDiffusionCppBackend(ILogger<StableDiffusionCppBackend> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (!_checkedAvailability)
        {
            try
            {
                // Try to initialize events — this loads the native library
                StableDiffusionCpp.InitializeEvents();
                _nativeAvailable = true;
                _logger.LogInformation("StableDiffusion.NET native library loaded successfully");
            }
            catch (Exception ex)
            {
                _nativeAvailable = false;
                _logger.LogWarning(ex, "StableDiffusion.NET native library not available — falling back to mock backend");
            }
            _checkedAvailability = true;
        }
        return Task.FromResult(_nativeAvailable);
    }

    public Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default)
    {
        _model?.Dispose();
        _model = null;

        _logger.LogInformation("Loading model: {Path}", request.CheckpointPath);

        var modelParams = DiffusionModelParameter.Create();
        modelParams.ModelPath = request.CheckpointPath;
        modelParams.FlashAttention = true;
        modelParams.ThreadCount = -1; // Use all physical cores

        if (!string.IsNullOrWhiteSpace(request.VaePath))
            modelParams.VaePath = request.VaePath;

        _model = new DiffusionModel(modelParams);

        // LoRAs are applied per-generation via ImageGenerationParameter.Loras
        // Store for later use if needed, but the SD.NET API supports per-generation LoRA
        _logger.LogInformation("Model loaded successfully");

        return Task.CompletedTask;
    }

    public Task<InferenceResult> GenerateAsync(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct = default)
    {
        if (_model is null)
            return Task.FromResult(new InferenceResult(false, [], "No model loaded"));

        var images = new List<GeneratedImageData>();

        // Hook into progress events
        void OnProgress(object? sender, StableDiffusionProgressEventArgs args)
        {
            progress.Report(new InferenceProgress(args.Step, args.Steps, $"Step {args.Step}/{args.Steps}"));
        }

        StableDiffusionCpp.Progress += OnProgress;

        try
        {
            for (int i = 0; i < request.BatchSize; i++)
            {
                ct.ThrowIfCancellationRequested();

                var seed = request.Seed == -1 ? Random.Shared.NextInt64() : request.Seed + i;
                var sw = Stopwatch.StartNew();

                var genParams = ImageGenerationParameter.TextToImage(request.PositivePrompt);
                genParams.NegativePrompt = request.NegativePrompt;
                genParams.Width = request.Width;
                genParams.Height = request.Height;
                genParams.Seed = seed;
                genParams.SampleParameter.Guidance.TxtCfg = (float)request.CfgScale;
                genParams.SampleParameter.SampleSteps = request.Steps;
                genParams.SampleParameter.SampleMethod = MapSampler(request.Sampler);
                genParams.SampleParameter.Scheduler = MapScheduler(request.Scheduler);

                var image = _model.GenerateImage(genParams);
                sw.Stop();

                if (image is null)
                {
                    _logger.LogWarning("Generation returned null for batch item {Index}", i);
                    continue;
                }

                // ToPng is a Windows-only extension from HPPH.System.Drawing (uses System.Drawing.Common)
                // This application targets Windows, so this is safe.
#pragma warning disable CA1416
                var pngBytes = image.ToPng();
#pragma warning restore CA1416
                images.Add(new GeneratedImageData(pngBytes, seed, sw.Elapsed.TotalSeconds));

                _logger.LogInformation("Generated image {Index}/{Batch} — seed: {Seed}, time: {Time:F1}s",
                    i + 1, request.BatchSize, seed, sw.Elapsed.TotalSeconds);
            }

            return Task.FromResult(new InferenceResult(true, images, null));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new InferenceResult(false, images, "Generation cancelled"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generation failed");
            return Task.FromResult(new InferenceResult(false, images, ex.Message));
        }
        finally
        {
            StableDiffusionCpp.Progress -= OnProgress;
        }
    }

    public Task UnloadModelAsync(CancellationToken ct = default)
    {
        _model?.Dispose();
        _model = null;
        _logger.LogInformation("Model unloaded");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _model?.Dispose();
        _model = null;
    }

    internal static SdSampler MapSampler(Sampler sampler) => sampler switch
    {
        Sampler.Euler => SdSampler.Euler,
        Sampler.EulerA => SdSampler.Euler_A,
        Sampler.DPMPlusPlus2M => SdSampler.DPMPP2M,
        Sampler.DPMPlusPlus2MKarras => SdSampler.DPMPP2M,
        Sampler.DPMPlusPlusSDE => SdSampler.DPMPP2SA,
        Sampler.DPMPlusPlusSDEKarras => SdSampler.DPMPP2SA,
        Sampler.DDIM => SdSampler.DDIM_Trailing,
        Sampler.UniPC => SdSampler.IPNDM,
        Sampler.LMS => SdSampler.LCM,
        Sampler.Heun => SdSampler.Heun,
        Sampler.DPM2 => SdSampler.DPM2,
        Sampler.DPM2A => SdSampler.DPM2,
        _ => SdSampler.Default
    };

    internal static SdScheduler MapScheduler(Scheduler scheduler) => scheduler switch
    {
        Scheduler.Normal => SdScheduler.Discrete,
        Scheduler.Karras => SdScheduler.Karras,
        Scheduler.Exponential => SdScheduler.Exponential,
        Scheduler.SGMUniform => SdScheduler.SGM_Uniform,
        _ => SdScheduler.Default
    };
}
