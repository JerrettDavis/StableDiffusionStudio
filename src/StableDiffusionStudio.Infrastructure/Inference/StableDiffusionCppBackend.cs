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
                // Try to find and load the native library manually if auto-discovery fails.
                // The CUDA backend package puts the DLL in runtimes/win-x64/native/cuda12/
                // but the auto-resolver only finds it if CUDA_PATH env var is set.
                TryLoadNativeLibrary();

                // Initialize events — this triggers the P/Invoke that confirms the library loaded
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

    private void TryLoadNativeLibrary()
    {
        // Search for the native library in known locations relative to the app base directory
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "cuda12", "stable-diffusion.dll"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "cpu", "stable-diffusion.dll"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "vulkan", "stable-diffusion.dll"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "stable-diffusion.dll"),
            Path.Combine(baseDir, "stable-diffusion.dll"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                _logger.LogInformation("Found native library at: {Path}", path);
                if (StableDiffusionCpp.LoadNativeLibrary(path))
                {
                    _logger.LogInformation("Loaded native library from: {Path}", path);
                    return;
                }
                _logger.LogWarning("Found but failed to load native library from: {Path}", path);
            }
        }

        // Also add the base directory to search paths so the Backends class can find it
        Backends.SearchPaths.Add(baseDir);
        _logger.LogDebug("Added {BaseDir} to Backends.SearchPaths", baseDir);
    }

    public Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default)
    {
        _model?.Dispose();
        _model = null;

        var fileName = Path.GetFileName(request.CheckpointPath);
        var fileNameLower = fileName.ToLowerInvariant();
        _logger.LogInformation("Loading model: {FileName} from {Path}", fileName, request.CheckpointPath);

        var modelParams = DiffusionModelParameter.Create();
        modelParams.FlashAttention = true;

        // Detect model type from filename and configure accordingly
        var isFlux = fileNameLower.Contains("flux");
        var isDiffusionModel = fileNameLower.Contains("_dev") || fileNameLower.Contains("_schnell");

        if (isFlux && isDiffusionModel)
        {
            // Flux models: use DiffusionModelPath for standalone diffusion models
            modelParams.DiffusionModelPath = request.CheckpointPath;
            modelParams.VaeTiling = true; // Flux needs tiling for memory
            modelParams.OffloadParamsToCPU = true; // Help with large models
            _logger.LogInformation("Configured as Flux diffusion model");
        }
        else
        {
            // SD 1.5, SDXL, and other standard models: use ModelPath
            modelParams.ModelPath = request.CheckpointPath;
        }

        if (!string.IsNullOrWhiteSpace(request.VaePath))
            modelParams.VaePath = request.VaePath;

        try
        {
            _model = new DiffusionModel(modelParams);
            _logger.LogInformation("Model loaded successfully: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model: {FileName}", fileName);

            // If Flux failed with DiffusionModelPath, try with ModelPath as fallback
            if (isFlux && isDiffusionModel)
            {
                _logger.LogInformation("Retrying Flux model with ModelPath instead of DiffusionModelPath");
                modelParams.DiffusionModelPath = string.Empty;
                modelParams.ModelPath = request.CheckpointPath;
                try
                {
                    _model = new DiffusionModel(modelParams);
                    _logger.LogInformation("Model loaded successfully on retry: {FileName}", fileName);
                    return Task.CompletedTask;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "Retry also failed for {FileName}", fileName);
                }
            }

            throw new InvalidOperationException(
                $"Failed to load model: {fileName}. " +
                $"Ensure the model file is a valid .safetensors or .gguf checkpoint. " +
                $"Flux GGUF models may need additional components (CLIP, T5XXL, VAE). " +
                $"Error: {ex.Message}", ex);
        }

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
