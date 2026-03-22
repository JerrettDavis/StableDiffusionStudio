using System.Text.Json;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Workflows;

/// <summary>
/// Upscale node. Takes an image and produces a higher-resolution version
/// using img2img with scaled-up dimensions (same approach as hires-fix).
/// </summary>
public class UpscaleNodePlugin : IWorkflowNodePlugin
{
    public string PluginId => "core.upscale";
    public string DisplayName => "Upscale";
    public string Category => "Processing";
    public string Description => "Upscale an image using img2img at higher resolution.";
    public string Icon => "Icons.Material.Filled.ZoomIn";

    public IReadOnlyList<WorkflowPortDefinition> InputPorts =>
    [
        new("image", WorkflowDataType.Image, Required: true)
    ];

    public IReadOnlyList<WorkflowPortDefinition> OutputPorts =>
    [
        new("image", WorkflowDataType.Image)
    ];

    public async Task<Dictionary<string, WorkflowData>> ExecuteAsync(
        IReadOnlyDictionary<string, WorkflowData> inputs,
        string? config,
        WorkflowExecutionContext context,
        CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("image", out var imageData) || imageData.ImageBytes is null)
            throw new InvalidOperationException("Upscale node requires an image input.");

        var upscaleConfig = JsonSerializer.Deserialize<UpscaleConfig>(config ?? "{}")
            ?? new UpscaleConfig();

        // Get source image dimensions
        int srcWidth, srcHeight;
        using (var ms = new MemoryStream(imageData.ImageBytes))
        {
#pragma warning disable CA1416
            using var img = System.Drawing.Image.FromStream(ms, false, false);
            srcWidth = img.Width;
            srcHeight = img.Height;
#pragma warning restore CA1416
        }

        var targetWidth = (int)(srcWidth * upscaleConfig.ScaleFactor);
        var targetHeight = (int)(srcHeight * upscaleConfig.ScaleFactor);
        // Round to nearest multiple of 64
        targetWidth = (targetWidth / 64) * 64;
        targetHeight = (targetHeight / 64) * 64;
        if (targetWidth < 64) targetWidth = 64;
        if (targetHeight < 64) targetHeight = 64;

        var parameters = !string.IsNullOrWhiteSpace(upscaleConfig.ParametersJson)
            ? JsonSerializer.Deserialize<GenerationParameters>(upscaleConfig.ParametersJson)
            : null;

        var request = new InferenceRequest(
            parameters?.PositivePrompt ?? "",
            parameters?.NegativePrompt ?? "",
            parameters?.Sampler ?? Domain.Enums.Sampler.EulerA,
            parameters?.Scheduler ?? Domain.Enums.Scheduler.Normal,
            upscaleConfig.Steps,
            parameters?.CfgScale ?? 7.0,
            Random.Shared.NextInt64(),
            targetWidth,
            targetHeight,
            1,
            parameters?.ClipSkip ?? 1,
            0.0,
            imageData.ImageBytes,
            upscaleConfig.DenoisingStrength);

        var progress = new Progress<InferenceProgress>(p =>
        {
            context.Progress.Report(new WorkflowStepProgress(
                context.NodeId, p.Step, p.TotalSteps, "Upscaling"));
        });

        var result = await context.InferenceBackend.GenerateAsync(request, progress, ct);

        if (result.Images.Count == 0)
            throw new InvalidOperationException("Upscale produced no images.");

        var outputDir = Path.Combine(context.AppPaths.AssetsDirectory, "workflows", context.WorkflowRunId.ToString());
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{context.NodeId}.png");
        await File.WriteAllBytesAsync(outputPath, result.Images[0].ImageBytes, ct);

        return new Dictionary<string, WorkflowData>
        {
            ["image"] = WorkflowData.FromImage(result.Images[0].ImageBytes)
        };
    }

    private record UpscaleConfig
    {
        public double ScaleFactor { get; init; } = 2.0;
        public int Steps { get; init; } = 20;
        public double DenoisingStrength { get; init; } = 0.55;
        public string? ParametersJson { get; init; }
    }
}
