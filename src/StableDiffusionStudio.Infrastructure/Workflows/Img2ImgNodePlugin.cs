using System.Text.Json;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Infrastructure.Workflows;

/// <summary>
/// Image-to-image transformation node. Requires an image input.
/// If no upstream connection, the image must be provided as a workflow entry point.
/// </summary>
public class Img2ImgNodePlugin : IWorkflowNodePlugin
{
    public string PluginId => "core.img2img";
    public string DisplayName => "Img2Img";
    public string Category => "Generation";
    public string Description => "Transform an image using a prompt and generation parameters.";
    public string Icon => "Icons.Material.Filled.Transform";

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
            throw new InvalidOperationException("Img2Img node requires an image input.");

        var parameters = DeserializeParameters(config);

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
            ImageInputMode: parameters.ImageInputMode);

        var progress = new Progress<InferenceProgress>(p =>
        {
            context.Progress.Report(new WorkflowStepProgress(
                context.NodeId, p.Step, p.TotalSteps, "Transforming"));
        });

        var result = await context.InferenceBackend.GenerateAsync(request, progress, ct);

        if (result.Images.Count == 0)
            throw new InvalidOperationException("Img2Img produced no images.");

        var outputDir = Path.Combine(context.AppPaths.AssetsDirectory, "workflows", context.WorkflowRunId.ToString());
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{context.NodeId}.png");
        await File.WriteAllBytesAsync(outputPath, result.Images[0].ImageBytes, ct);

        return new Dictionary<string, WorkflowData>
        {
            ["image"] = WorkflowData.FromImage(result.Images[0].ImageBytes)
        };
    }

    private static GenerationParameters DeserializeParameters(string? config)
    {
        if (string.IsNullOrWhiteSpace(config))
            throw new InvalidOperationException("Img2Img node requires parameters configuration.");
        return JsonSerializer.Deserialize<GenerationParameters>(config)
            ?? throw new InvalidOperationException("Failed to deserialize generation parameters.");
    }
}
