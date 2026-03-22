using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StableDiffusion.NET;
using HPPH.System.Drawing;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using Sampler = StableDiffusionStudio.Domain.Enums.Sampler;
using Scheduler = StableDiffusionStudio.Domain.Enums.Scheduler;
using SdSampler = StableDiffusion.NET.Sampler;
using SdScheduler = StableDiffusion.NET.Scheduler;

namespace StableDiffusionStudio.Infrastructure.Inference;

public class StableDiffusionCppBackend : IInferenceBackend, IDisposable
{
    private readonly ILogger<StableDiffusionCppBackend> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DiffusionModel? _model;
    private string? _loadedModelPath;
    private bool _nativeAvailable;
    private bool _checkedAvailability;
    private bool _isFluxModel;
    private bool _loadedFromCuda;

    public string BackendId => "stable-diffusion-cpp";
    public string DisplayName => "Stable Diffusion (C++)";
    public InferenceCapabilities Capabilities => new(
        SupportedFamilies: [ModelFamily.SD15, ModelFamily.SDXL, ModelFamily.Flux],
        SupportedSamplers: [Sampler.Euler, Sampler.EulerA, Sampler.DPMPlusPlus2M, Sampler.DPMPlusPlusSDE,
                           Sampler.DDIM, Sampler.Heun, Sampler.DPM2, Sampler.LMS, Sampler.UniPC],
        MaxWidth: 2048, MaxHeight: 2048,
        SupportsLoRA: true, SupportsVAE: true);

    public StableDiffusionCppBackend(ILogger<StableDiffusionCppBackend> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
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
        var baseDir = AppContext.BaseDirectory;
        var nativeDir = Path.Combine(baseDir, "runtimes", "win-x64", "native");

        // Add the native directory to PATH so CUDA runtime DLLs (cudart64_12.dll, cublas64_12.dll)
        // can be found when loading the CUDA build of stable-diffusion.dll from the cuda12/ subfolder
        if (Directory.Exists(nativeDir))
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!currentPath.Contains(nativeDir, StringComparison.OrdinalIgnoreCase))
            {
                Environment.SetEnvironmentVariable("PATH", $"{nativeDir};{currentPath}");
                _logger.LogInformation("Added {Dir} to PATH for CUDA runtime DLL resolution", nativeDir);
            }
        }

        // Priority: CUDA (fastest for NVIDIA GPUs) → Vulkan (universal GPU) → CPU (fallback)
        var candidates = new[]
        {
            Path.Combine(nativeDir, "cuda12", "stable-diffusion.dll"),
            Path.Combine(nativeDir, "vulkan", "stable-diffusion.dll"),
            Path.Combine(nativeDir, "avx512", "stable-diffusion.dll"),
            Path.Combine(nativeDir, "avx2", "stable-diffusion.dll"),
            Path.Combine(nativeDir, "avx", "stable-diffusion.dll"),
            Path.Combine(nativeDir, "cpu", "stable-diffusion.dll"),
            Path.Combine(nativeDir, "stable-diffusion.dll"),
            Path.Combine(baseDir, "stable-diffusion.dll"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                _logger.LogInformation("Trying native library: {Path}", path);
                if (StableDiffusionCpp.LoadNativeLibrary(path))
                {
                    _loadedFromCuda = path.Contains("cuda", StringComparison.OrdinalIgnoreCase);
                    _logger.LogInformation("Loaded native library from: {Path} (CUDA={IsCuda})", path, _loadedFromCuda);
                    return;
                }
                _logger.LogWarning("Found but failed to load: {Path}", path);
            }
        }

        // Also add the base directory to search paths so the Backends class can find it
        Backends.SearchPaths.Add(baseDir);
        _logger.LogDebug("Added {BaseDir} to Backends.SearchPaths", baseDir);
    }

    public async Task LoadModelAsync(ModelLoadRequest request, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await LoadModelCoreAsync(request, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task LoadModelCoreAsync(ModelLoadRequest request, CancellationToken ct)
    {
        // Reuse loaded model if the checkpoint path hasn't changed
        if (_model is not null && _loadedModelPath == request.CheckpointPath)
        {
            _logger.LogInformation("Model already loaded, reusing: {Path}", request.CheckpointPath);
            return;
        }

        // Fully unload previous model to free GPU VRAM before loading new one
        if (_model is not null)
        {
            _logger.LogInformation("Unloading previous model to free VRAM");
            _model.Dispose();
            _model = null;
            _loadedModelPath = null;
            GC.Collect(); // Encourage native memory release
            GC.WaitForPendingFinalizers();
        }

        var fileName = Path.GetFileName(request.CheckpointPath);
        var fileNameLower = fileName.ToLowerInvariant();
        _logger.LogInformation("Loading model: {FileName} from {Path}", fileName, request.CheckpointPath);

        // Read inference settings from the database via a scoped provider
        InferenceSettings settings;
        using (var scope = _scopeFactory.CreateScope())
        {
            var settingsProvider = scope.ServiceProvider.GetRequiredService<IInferenceSettingsProvider>();
            settings = await settingsProvider.GetSettingsAsync(ct);
        }

        var modelParams = DiffusionModelParameter.Create();
        modelParams.ThreadCount = settings.EffectiveThreadCount;
        modelParams.FlashAttention = settings.FlashAttention;
        modelParams.DiffusionFlashAttention = settings.DiffusionFlashAttention;
        modelParams.VaeTiling = settings.VaeTiling;
        // Always keep VAE encoder available — img2img and inpainting require encoding
        // the init image to latent space. VaeDecodeOnly=true skips building the encoder
        // graph, which causes GGML_ASSERT failures on img2img.
        modelParams.VaeDecodeOnly = false;
        modelParams.KeepClipOnCPU = settings.KeepClipOnCPU;
        modelParams.KeepVaeOnCPU = settings.KeepVaeOnCPU;
        modelParams.KeepControlNetOnCPU = settings.KeepControlNetOnCPU;
        modelParams.EnableMmap = settings.EnableMmap;

        // Detect model type from filename
        var isFlux = fileNameLower.Contains("flux");
        _isFluxModel = isFlux;

        if (isFlux)
        {
            // Flux requires DiffusionModelPath (not ModelPath) + CLIP-L + T5-XXL + VAE
            modelParams.ModelPath = string.Empty;
            modelParams.DiffusionModelPath = request.CheckpointPath;

            if (request.ClipLPath is not null)
                modelParams.ClipLPath = request.ClipLPath;
            if (request.T5xxlPath is not null)
                modelParams.T5xxlPath = request.T5xxlPath;
            if (!string.IsNullOrWhiteSpace(request.VaePath))
                modelParams.VaePath = request.VaePath;

            // CRITICAL: Do NOT offload to CPU on hybrid CPUs (Intel 12th-14th gen)
            // stable-diffusion.cpp has a known AVX512 false detection bug (#1343)
            // that causes illegal instruction crashes when CPU offloading runs on
            // E-cores that don't support AVX512. Keep everything on GPU.
            modelParams.OffloadParamsToCPU = false;
            modelParams.KeepClipOnCPU = false;
            modelParams.KeepVaeOnCPU = false;
            modelParams.KeepControlNetOnCPU = false;

            _logger.LogInformation("Flux model configured: DiffusionModel={Model}, CLIP-L={ClipL}, T5-XXL={T5xxl}, VAE={Vae}",
                Path.GetFileName(request.CheckpointPath),
                request.ClipLPath != null ? Path.GetFileName(request.ClipLPath) : "NONE",
                request.T5xxlPath != null ? Path.GetFileName(request.T5xxlPath) : "NONE",
                request.VaePath != null ? Path.GetFileName(request.VaePath) : "NONE");
        }
        else
        {
            modelParams.ModelPath = request.CheckpointPath;

            if (_loadedFromCuda)
            {
                // CUDA + CPU offloading crashes on hybrid Intel CPUs due to AVX512 false
                // detection in stable-diffusion.cpp (issue #1343). Disable all CPU offloading
                // when using CUDA — the GPU handles everything.
                _logger.LogInformation("CUDA backend active — keeping all computation on GPU");
                modelParams.OffloadParamsToCPU = false;
                modelParams.KeepClipOnCPU = false;
                modelParams.KeepVaeOnCPU = false;
                modelParams.KeepControlNetOnCPU = false;
            }
            else
            {
                modelParams.OffloadParamsToCPU = settings.OffloadParamsToCPU;
            }

            if (!string.IsNullOrWhiteSpace(request.VaePath))
                modelParams.VaePath = request.VaePath;
        }

        _logger.LogInformation("Model params: FlashAttn={FA}, DiffFlashAttn={DFA}, VaeTiling={VT}, ClipOnCPU={CC}, VaeOnCPU={VC}",
            modelParams.FlashAttention, modelParams.DiffusionFlashAttention,
            modelParams.VaeTiling, modelParams.KeepClipOnCPU, modelParams.KeepVaeOnCPU);

        try
        {
            _model = new DiffusionModel(modelParams);
            _loadedModelPath = request.CheckpointPath;
            _logger.LogInformation("Model loaded successfully: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model: {FileName}", fileName);

            if (isFlux)
            {
                var missing = new List<string>();
                if (request.ClipLPath == null) missing.Add("CLIP-L text encoder (clip_l.safetensors)");
                if (request.T5xxlPath == null) missing.Add("T5-XXL text encoder (t5xxl*.gguf or *.safetensors)");
                if (string.IsNullOrWhiteSpace(request.VaePath)) missing.Add("Flux VAE (ae.safetensors)");

                var missingMsg = missing.Count > 0
                    ? $" Missing components: {string.Join(", ", missing)}. Add VAE and text_encoder folders to your storage roots in Settings."
                    : "";

                throw new InvalidOperationException(
                    $"Failed to load Flux model: {fileName}.{missingMsg} Error: {ex.Message}", ex);
            }

            throw new InvalidOperationException(
                $"Failed to load model: {fileName}. " +
                $"Error: {ex.Message}", ex);
        }
    }

    public async Task<InferenceResult> GenerateAsync(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            // Run on a background thread so Progress<T> callbacks can execute
            // on the captured SynchronizationContext while generation runs
            return await Task.Run(() => GenerateCore(request, progress, ct), ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private InferenceResult GenerateCore(InferenceRequest request, IProgress<InferenceProgress> progress, CancellationToken ct)
    {
        if (_model is null)
            return new InferenceResult(false, [], "No model loaded");

        var images = new List<GeneratedImageData>();

        // Enable preview decoding during generation
        // Proj = latent projection (fast, approximate, no extra model needed)
        // TAE = Tiny AutoEncoder (fast, good quality, needs TAESD model)
        // VAE = Full VAE decode (slow, best quality, uses loaded VAE)
        // Try Proj first — if the backend supports it, we get fast previews
        try
        {
            StableDiffusionCpp.EnablePreview(StableDiffusion.NET.Preview.Proj, 1, true, false);
            _logger.LogInformation("Preview mode: Proj (latent projection)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enable Proj preview — previews will not be available");
        }

        byte[]? latestPreviewBytes = null;

        int previewCount = 0;
        void OnPreview(object? sender, StableDiffusionPreviewEventArgs args)
        {
            try
            {
#pragma warning disable CA1416
                latestPreviewBytes = args.Image.ToPng();
#pragma warning restore CA1416
                previewCount++;
                _logger.LogInformation("Preview received: step {Step}, {Bytes} bytes (total previews: {Count})",
                    args.Step, latestPreviewBytes?.Length ?? 0, previewCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Preview conversion failed at step {Step}", args.Step);
            }
        }

        // Hook into progress and preview events
        int progressCount = 0;
        void OnProgress(object? sender, StableDiffusionProgressEventArgs args)
        {
            var preview = latestPreviewBytes;
            latestPreviewBytes = null; // Consume the preview
            progressCount++;
            _logger.LogInformation("Progress: step {Step}/{Total}, hasPreview={HasPreview} (progress #{Count})",
                args.Step, args.Steps, preview is not null, progressCount);
            progress.Report(new InferenceProgress(args.Step, args.Steps, $"Step {args.Step}/{args.Steps}", preview));
        }

        StableDiffusionCpp.Preview += OnPreview;
        StableDiffusionCpp.Progress += OnProgress;

        try
        {
            for (int i = 0; i < request.BatchSize; i++)
            {
                ct.ThrowIfCancellationRequested();

                var seed = request.Seed == -1 ? Random.Shared.NextInt64() : request.Seed + i;
                var sw = Stopwatch.StartNew();

                ImageGenerationParameter genParams;
                if (request.InitImage is not null && request.DenoisingStrength < 1.0)
                {
                    // img2img or inpainting: resize init image to match target dimensions.
                    // stable-diffusion.cpp asserts image.width == tensor->ne[0] — the init image
                    // must exactly match the generation width/height.
                    var initImage = LoadAndResizeImage(request.InitImage, request.Width, request.Height, request.ImageInputMode);
                    genParams = ImageGenerationParameter.ImageToImage(request.PositivePrompt, initImage);
                    genParams.Strength = (float)request.DenoisingStrength;

                    // If a mask is provided, set it for inpainting (white = regenerate, black = keep)
                    if (request.MaskImage is not null)
                    {
                        genParams.MaskImage = LoadAndResizeImage(request.MaskImage, request.Width, request.Height, request.ImageInputMode);
                        _logger.LogInformation("Using inpainting pipeline with mask and denoising strength {Strength}", request.DenoisingStrength);
                    }
                    else
                    {
                        _logger.LogInformation("Using img2img pipeline with denoising strength {Strength}", request.DenoisingStrength);
                    }
                }
                else
                {
                    genParams = ImageGenerationParameter.TextToImage(request.PositivePrompt);
                }
                genParams.NegativePrompt = request.NegativePrompt;
                genParams.Width = request.Width;
                genParams.Height = request.Height;
                genParams.Seed = seed;
                genParams.ClipSkip = request.ClipSkip;
                genParams.SampleParameter.SampleSteps = request.Steps;
                genParams.SampleParameter.SampleMethod = MapSampler(request.Sampler);
                genParams.SampleParameter.Scheduler = MapScheduler(request.Scheduler);
                genParams.SampleParameter.Eta = (float)request.Eta;

                if (_isFluxModel)
                {
                    // Flux: CFG scale goes to DistilledGuidance, TxtCfg should be 1.0
                    genParams.SampleParameter.Guidance.TxtCfg = 1.0f;
                    genParams.SampleParameter.Guidance.DistilledGuidance = (float)request.CfgScale;
                }
                else
                {
                    // SD 1.5 / SDXL: CFG scale goes to TxtCfg, DistilledGuidance should be 1.0
                    genParams.SampleParameter.Guidance.TxtCfg = (float)request.CfgScale;
                    genParams.SampleParameter.Guidance.DistilledGuidance = 1.0f;
                }

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

            return new InferenceResult(true, images, null);
        }
        catch (OperationCanceledException)
        {
            return new InferenceResult(false, images, "Generation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generation failed");
            return new InferenceResult(false, images, ex.Message);
        }
        finally
        {
            StableDiffusionCpp.Progress -= OnProgress;
            StableDiffusionCpp.Preview -= OnPreview;
            StableDiffusionCpp.EnablePreview(StableDiffusion.NET.Preview.None, 0, false, false);
        }
    }

    public Task UnloadModelAsync(CancellationToken ct = default)
    {
        _model?.Dispose();
        _model = null;
        _loadedModelPath = null;
        _logger.LogInformation("Model unloaded");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
        _model?.Dispose();
        _model = null;
        _loadedModelPath = null;
    }

    /// <summary>
    /// Converts PNG bytes to an HPPH IImage suitable for img2img generation.
    /// Uses System.Drawing.Bitmap (Windows-only) and the HPPH.System.Drawing ToImage() extension.
    /// </summary>
#pragma warning disable CA1416 // Platform compatibility — this application targets Windows
    private static HPPH.IImage LoadImageFromBytes(byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        using var bitmap = new System.Drawing.Bitmap(ms);
        return bitmap.ToImage();
    }

    /// <summary>
    /// Loads an image from bytes and fits it to the target dimensions using the specified mode.
    /// stable-diffusion.cpp requires the init image to exactly match the generation
    /// width/height (asserts image.width == tensor->ne[0]).
    /// </summary>
    private static HPPH.IImage LoadAndResizeImage(byte[] imageBytes, int targetWidth, int targetHeight,
        Domain.Enums.ImageInputMode mode = Domain.Enums.ImageInputMode.Scale)
    {
        using var ms = new MemoryStream(imageBytes);
        using var original = new System.Drawing.Bitmap(ms);

        if (original.Width == targetWidth && original.Height == targetHeight)
            return original.ToImage();

        using var result = new System.Drawing.Bitmap(targetWidth, targetHeight);
        using var g = System.Drawing.Graphics.FromImage(result);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        switch (mode)
        {
            case Domain.Enums.ImageInputMode.Scale:
            {
                // Scale longest edge to fit, maintain aspect ratio, pad remainder with black
                g.Clear(System.Drawing.Color.Black);
                var scale = Math.Min((double)targetWidth / original.Width, (double)targetHeight / original.Height);
                var scaledW = (int)(original.Width * scale);
                var scaledH = (int)(original.Height * scale);
                var offsetX = (targetWidth - scaledW) / 2;
                var offsetY = (targetHeight - scaledH) / 2;
                g.DrawImage(original, offsetX, offsetY, scaledW, scaledH);
                break;
            }
            case Domain.Enums.ImageInputMode.CenterCrop:
            {
                // Scale to cover target, then center-crop the excess
                var scale = Math.Max((double)targetWidth / original.Width, (double)targetHeight / original.Height);
                var scaledW = (int)(original.Width * scale);
                var scaledH = (int)(original.Height * scale);
                var offsetX = (targetWidth - scaledW) / 2;
                var offsetY = (targetHeight - scaledH) / 2;
                g.DrawImage(original, offsetX, offsetY, scaledW, scaledH);
                break;
            }
            case Domain.Enums.ImageInputMode.Resize:
            default:
            {
                // Stretch to fit exactly — no aspect ratio preservation
                g.DrawImage(original, 0, 0, targetWidth, targetHeight);
                break;
            }
        }

        return result.ToImage();
    }
#pragma warning restore CA1416

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
