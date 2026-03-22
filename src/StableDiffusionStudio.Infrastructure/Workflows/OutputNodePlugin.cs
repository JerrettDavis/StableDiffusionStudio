using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Workflows;

/// <summary>
/// Terminal node that marks an image as a final workflow output.
/// Saves the image to the workflow results directory.
/// </summary>
public class OutputNodePlugin : IWorkflowNodePlugin
{
    public string PluginId => "core.output";
    public string DisplayName => "Output";
    public string Category => "Control";
    public string Description => "Marks an image as a final workflow result.";
    public string Icon => "Icons.Material.Filled.SaveAlt";

    public IReadOnlyList<WorkflowPortDefinition> InputPorts =>
    [
        new("image", WorkflowDataType.Image, Required: true)
    ];

    public IReadOnlyList<WorkflowPortDefinition> OutputPorts => [];

    public async Task<Dictionary<string, WorkflowData>> ExecuteAsync(
        IReadOnlyDictionary<string, WorkflowData> inputs,
        string? config,
        WorkflowExecutionContext context,
        CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("image", out var imageData) || imageData.ImageBytes is null)
            throw new InvalidOperationException("Output node requires an image input.");

        // Save the final output image
        var outputDir = Path.Combine(context.AppPaths.AssetsDirectory, "workflows", context.WorkflowRunId.ToString());
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"output-{context.NodeId}.png");
        await File.WriteAllBytesAsync(outputPath, imageData.ImageBytes, ct);

        context.Progress.Report(new WorkflowStepProgress(context.NodeId, 1, 1, "Saved"));

        // Output nodes produce no downstream data
        return new Dictionary<string, WorkflowData>();
    }
}
