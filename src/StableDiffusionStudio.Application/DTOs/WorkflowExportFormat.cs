namespace StableDiffusionStudio.Application.DTOs;

/// <summary>Portable JSON format for workflow import/export.</summary>
public record WorkflowExportFormat
{
    public string FormatVersion { get; init; } = "1.0";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public string? CanvasStateJson { get; init; }
    public IReadOnlyList<ExportedNode> Nodes { get; init; } = [];
    public IReadOnlyList<ExportedEdge> Edges { get; init; } = [];
}

public record ExportedNode(
    string LocalId, string PluginId, string Label,
    double PositionX, double PositionY,
    string? ParametersJson, string? ConfigJson, int? MaxIterations);

public record ExportedEdge(
    string SourceLocalId, string SourcePort, string TargetLocalId, string TargetPort);
