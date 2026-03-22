using System.Text.Json;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Workflows;

/// <summary>
/// Conditional branching node. Evaluates an expression against the input image's metadata
/// and routes to either the "pass" or "fail" output port.
/// Supports loop iteration via MaxIterations on the node.
/// </summary>
public class ConditionalNodePlugin : IWorkflowNodePlugin
{
    public string PluginId => "core.conditional";
    public string DisplayName => "Conditional";
    public string Category => "Control";
    public string Description => "Route images based on conditions. Supports looping.";
    public string Icon => "Icons.Material.Filled.CallSplit";

    public IReadOnlyList<WorkflowPortDefinition> InputPorts =>
    [
        new("image", WorkflowDataType.Image, Required: true)
    ];

    public IReadOnlyList<WorkflowPortDefinition> OutputPorts =>
    [
        new("pass", WorkflowDataType.Image),
        new("fail", WorkflowDataType.Image)
    ];

    public Task<Dictionary<string, WorkflowData>> ExecuteAsync(
        IReadOnlyDictionary<string, WorkflowData> inputs,
        string? config,
        WorkflowExecutionContext context,
        CancellationToken ct = default)
    {
        if (!inputs.TryGetValue("image", out var imageData) || imageData.ImageBytes is null)
            throw new InvalidOperationException("Conditional node requires an image input.");

        var condConfig = JsonSerializer.Deserialize<ConditionalConfig>(config ?? "{}")
            ?? new ConditionalConfig();

        var passed = EvaluateCondition(condConfig, imageData);

        context.Progress.Report(new WorkflowStepProgress(
            context.NodeId, 1, 1, passed ? "Condition: PASS" : "Condition: FAIL"));

        var outputs = new Dictionary<string, WorkflowData>();
        if (passed)
            outputs["pass"] = imageData;
        else
            outputs["fail"] = imageData;

        return Task.FromResult(outputs);
    }

    private static bool EvaluateCondition(ConditionalConfig config, WorkflowData imageData)
    {
        if (string.IsNullOrWhiteSpace(config.Expression))
            return true; // No condition = always pass

        // Simple expression evaluation: "field operator value"
        // Supported: image_size > N, has_image, always_pass, always_fail
        var expr = config.Expression.Trim().ToLowerInvariant();

        if (expr == "always_pass") return true;
        if (expr == "always_fail") return false;
        if (expr == "has_image") return imageData.ImageBytes is not null;

        // image_size > N (bytes)
        if (expr.StartsWith("image_size") && imageData.ImageBytes is not null)
        {
            var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && double.TryParse(parts[2], out var threshold))
            {
                var size = (double)imageData.ImageBytes.Length;
                return parts[1] switch
                {
                    ">" => size > threshold,
                    "<" => size < threshold,
                    ">=" => size >= threshold,
                    "<=" => size <= threshold,
                    "==" => Math.Abs(size - threshold) < 0.001,
                    _ => true
                };
            }
        }

        // Metadata-based conditions: "metadata.key operator value"
        if (expr.StartsWith("metadata.") && imageData.Metadata is not null)
        {
            var withoutPrefix = expr["metadata.".Length..];
            var parts = withoutPrefix.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 && imageData.Metadata.TryGetValue(parts[0], out var metaValue))
            {
                if (double.TryParse(metaValue, out var numVal) && double.TryParse(parts[2], out var threshold))
                {
                    return parts[1] switch
                    {
                        ">" => numVal > threshold,
                        "<" => numVal < threshold,
                        ">=" => numVal >= threshold,
                        "<=" => numVal <= threshold,
                        "==" => Math.Abs(numVal - threshold) < 0.001,
                        _ => true
                    };
                }
                // String comparison
                return parts[1] switch
                {
                    "==" => metaValue.Equals(parts[2], StringComparison.OrdinalIgnoreCase),
                    "!=" => !metaValue.Equals(parts[2], StringComparison.OrdinalIgnoreCase),
                    _ => true
                };
            }
        }

        return true; // Unknown expressions default to pass
    }

    private record ConditionalConfig
    {
        public string? Expression { get; init; }
    }
}
