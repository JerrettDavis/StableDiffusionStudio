using StableDiffusionStudio.Application.DTOs;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IWorkflowService
{
    Task<WorkflowDto> CreateAsync(string name, string? description = null, CancellationToken ct = default);
    Task<WorkflowDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowListItemDto>> ListAsync(CancellationToken ct = default);
    Task UpdateAsync(Guid id, string name, string? description, string? canvasStateJson, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Graph editing
    Task<WorkflowNodeDto> AddNodeAsync(Guid workflowId, string pluginId, string label,
        double positionX, double positionY, string? parametersJson = null, string? configJson = null,
        CancellationToken ct = default);
    Task RemoveNodeAsync(Guid workflowId, Guid nodeId, CancellationToken ct = default);
    Task UpdateNodeAsync(Guid workflowId, Guid nodeId, string label, string? parametersJson,
        string? configJson, int? maxIterations = null, CancellationToken ct = default);
    Task UpdateNodePositionAsync(Guid workflowId, Guid nodeId, double x, double y, CancellationToken ct = default);

    Task<WorkflowEdgeDto> AddEdgeAsync(Guid workflowId, Guid sourceNodeId, string sourcePort,
        Guid targetNodeId, string targetPort, CancellationToken ct = default);
    Task RemoveEdgeAsync(Guid workflowId, Guid edgeId, CancellationToken ct = default);

    // Execution
    Task<Guid> StartRunAsync(Guid workflowId, string? inputsJson = null, CancellationToken ct = default);
    Task<WorkflowRunDto?> GetRunAsync(Guid runId, CancellationToken ct = default);

    // Import/export
    Task<WorkflowExportFormat> ExportAsync(Guid workflowId, CancellationToken ct = default);
    Task<WorkflowDto> ImportAsync(WorkflowExportFormat data, CancellationToken ct = default);
    Task<WorkflowDto> DuplicateAsync(Guid workflowId, CancellationToken ct = default);

    // Plugin discovery
    IReadOnlyList<WorkflowNodePluginDto> GetAvailablePlugins();
}
