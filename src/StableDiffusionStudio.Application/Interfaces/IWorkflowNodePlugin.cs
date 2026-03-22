namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Contract for workflow node plugins. Every node type (built-in or third-party)
/// implements this interface. The workflow execution engine resolves plugins by PluginId.
/// </summary>
public interface IWorkflowNodePlugin
{
    /// <summary>Unique identifier, e.g. "core.generate", "core.img2img", "community.face-restore".</summary>
    string PluginId { get; }

    /// <summary>Display name shown in the node palette.</summary>
    string DisplayName { get; }

    /// <summary>Category for grouping in the palette (e.g. "Generation", "Processing", "Control").</summary>
    string Category { get; }

    /// <summary>Short description of what this node does.</summary>
    string Description { get; }

    /// <summary>MudBlazor icon identifier for the node palette.</summary>
    string Icon { get; }

    /// <summary>Input ports this node accepts.</summary>
    IReadOnlyList<WorkflowPortDefinition> InputPorts { get; }

    /// <summary>Output ports this node produces.</summary>
    IReadOnlyList<WorkflowPortDefinition> OutputPorts { get; }

    /// <summary>
    /// Execute this node's operation.
    /// </summary>
    /// <param name="inputs">Data from connected upstream nodes, keyed by input port name.</param>
    /// <param name="config">Node-specific configuration (serialized from the editor).</param>
    /// <param name="context">Execution context providing access to services.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Output data keyed by output port name.</returns>
    Task<Dictionary<string, WorkflowData>> ExecuteAsync(
        IReadOnlyDictionary<string, WorkflowData> inputs,
        string? config,
        WorkflowExecutionContext context,
        CancellationToken ct = default);
}

/// <summary>Defines a port (input or output) on a workflow node.</summary>
public record WorkflowPortDefinition(string Name, WorkflowDataType DataType, bool Required = true);

/// <summary>Types of data that can flow between workflow nodes.</summary>
public enum WorkflowDataType
{
    Image,
    Mask,
    Text,
    Number,
    Metadata
}

/// <summary>A unit of data flowing between workflow nodes.</summary>
public record WorkflowData(WorkflowDataType Type, byte[]? ImageBytes = null, string? TextValue = null,
    double? NumberValue = null, Dictionary<string, string>? Metadata = null)
{
    public static WorkflowData FromImage(byte[] bytes) => new(WorkflowDataType.Image, ImageBytes: bytes);
    public static WorkflowData FromMask(byte[] bytes) => new(WorkflowDataType.Mask, ImageBytes: bytes);
    public static WorkflowData FromText(string text) => new(WorkflowDataType.Text, TextValue: text);
    public static WorkflowData FromNumber(double value) => new(WorkflowDataType.Number, NumberValue: value);
    public static WorkflowData FromMetadata(Dictionary<string, string> meta) => new(WorkflowDataType.Metadata, Metadata: meta);
}

/// <summary>
/// Context provided to plugins during execution, giving access to services
/// without plugins needing direct DI references.
/// </summary>
public class WorkflowExecutionContext
{
    public required IInferenceBackend InferenceBackend { get; init; }
    public required IModelCatalogRepository ModelRepository { get; init; }
    public required IAppPaths AppPaths { get; init; }
    public required Guid WorkflowRunId { get; init; }
    public required Guid NodeId { get; init; }
    public required IProgress<WorkflowStepProgress> Progress { get; init; }
}

/// <summary>Progress update from a workflow step.</summary>
public record WorkflowStepProgress(Guid NodeId, int CurrentStep, int TotalSteps, string? Phase = null, byte[]? PreviewImage = null);
