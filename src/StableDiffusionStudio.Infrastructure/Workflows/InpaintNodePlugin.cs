using System.Text.Json;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Workflows;

/// <summary>
/// Inpainting node. Takes an image and a mask, regenerates masked regions.
/// If no upstream mask connection, mask data must be embedded in node config.
/// </summary>
public class InpaintNodePlugin : IWorkflowNodePlugin
{
    public string PluginId => "core.inpaint";
    public string DisplayName => "Inpaint";
    public string Category => "Generation";
    public string Description => "Selectively regenerate parts of an image using a mask.";
    public string Icon => "Icons.Material.Filled.Brush";

    public IReadOnlyList<WorkflowPortDefinition> InputPorts =>
    [
        new("image", WorkflowDataType.Image, Required: true),
        new("mask", WorkflowDataType.Mask, Required: true)
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
            throw new InvalidOperationException("Inpaint node requires an image input.");
        if (!inputs.TryGetValue("mask", out var maskData) || maskData.ImageBytes is null)
            throw new InvalidOperationException("Inpaint node requires a mask input.");

        var parameters = JsonSerializer.Deserialize<GenerationParameters>(config ?? "")
            ?? throw new InvalidOperationException("Inpaint node requires parameters configuration.");

        var request = new InferenceRequest(
            parameters.PositivePrompt,
            parameters.NegativePrompt,
            parameters.Sampler,
            parameters.Scheduler,
            parameters.Steps,
            parameters.CfgScale,
            parameters.Seed == -1 ? Random.Shared.NextInt64() : parameters.Seed,
            parameters.Width,
            parameters.Height,
            1,
            parameters.ClipSkip,
            parameters.Eta,
            imageData.ImageBytes,
            parameters.DenoisingStrength,
            maskData.ImageBytes,
            parameters.ImageInputMode);

        var progress = new Progress<InferenceProgress>(p =>
        {
            context.Progress.Report(new WorkflowStepProgress(
                context.NodeId, p.Step, p.TotalSteps, "Inpainting"));
        });

        var result = await context.InferenceBackend.GenerateAsync(request, progress, ct);

        if (result.Images.Count == 0)
            throw new InvalidOperationException("Inpainting produced no images.");

        var outputDir = Path.Combine(context.AppPaths.AssetsDirectory, "workflows", context.WorkflowRunId.ToString());
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{context.NodeId}.png");
        await File.WriteAllBytesAsync(outputPath, result.Images[0].ImageBytes, ct);

        return new Dictionary<string, WorkflowData>
        {
            ["image"] = WorkflowData.FromImage(result.Images[0].ImageBytes)
        };
    }
}
