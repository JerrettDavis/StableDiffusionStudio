using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _repository;
    private readonly IJobQueue _jobQueue;
    private readonly IEnumerable<IWorkflowNodePlugin> _plugins;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(IWorkflowRepository repository, IJobQueue jobQueue,
        IEnumerable<IWorkflowNodePlugin> plugins, ILogger<WorkflowService> logger)
    {
        _repository = repository;
        _jobQueue = jobQueue;
        _plugins = plugins;
        _logger = logger;
    }

    public async Task<WorkflowDto> CreateAsync(string name, string? description = null, CancellationToken ct = default)
    {
        var workflow = Workflow.Create(name, description);
        await _repository.AddAsync(workflow, ct);
        return ToDto(workflow);
    }

    public async Task<WorkflowDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await _repository.GetByIdAsync(id, ct);
        return workflow is null ? null : ToDto(workflow);
    }

    public async Task<IReadOnlyList<WorkflowListItemDto>> ListAsync(CancellationToken ct = default)
    {
        var workflows = await _repository.ListAsync(ct);
        return workflows.Select(w => new WorkflowListItemDto(
            w.Id, w.Name, w.Description, w.IsTemplate, w.CreatedAt, w.UpdatedAt)).ToList();
    }

    public async Task UpdateAsync(Guid id, string name, string? description, string? canvasStateJson,
        CancellationToken ct = default)
    {
        var workflow = await _repository.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Workflow {id} not found.");
        workflow.Update(name, description, canvasStateJson);
        await _repository.UpdateAsync(workflow, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }

    // --- Node operations: create entities directly, bypass parent aggregate ---

    public async Task<WorkflowNodeDto> AddNodeAsync(Guid workflowId, string pluginId, string label,
        double positionX, double positionY, string? parametersJson = null, string? configJson = null,
        CancellationToken ct = default)
    {
        var node = WorkflowNode.Create(workflowId, pluginId, label, positionX, positionY, parametersJson, configJson);
        await _repository.AddNodeAsync(node, ct);
        return ToNodeDto(node);
    }

    public async Task RemoveNodeAsync(Guid workflowId, Guid nodeId, CancellationToken ct = default)
    {
        await _repository.RemoveNodeAsync(nodeId, ct);
    }

    public async Task UpdateNodeAsync(Guid workflowId, Guid nodeId, string label, string? parametersJson,
        string? configJson, int? maxIterations = null, CancellationToken ct = default)
    {
        var workflow = await _repository.GetByIdAsync(workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found.");
        var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId)
            ?? throw new InvalidOperationException($"Node {nodeId} not found.");
        node.UpdateConfig(label, parametersJson, configJson, maxIterations);
        await _repository.UpdateNodeAsync(node, ct);
    }

    public async Task UpdateNodePositionAsync(Guid workflowId, Guid nodeId, double x, double y,
        CancellationToken ct = default)
    {
        var workflow = await _repository.GetByIdAsync(workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found.");
        var node = workflow.Nodes.FirstOrDefault(n => n.Id == nodeId)
            ?? throw new InvalidOperationException($"Node {nodeId} not found.");
        node.UpdatePosition(x, y);
        await _repository.UpdateNodeAsync(node, ct);
    }

    // --- Edge operations ---

    public async Task<WorkflowEdgeDto> AddEdgeAsync(Guid workflowId, Guid sourceNodeId, string sourcePort,
        Guid targetNodeId, string targetPort, CancellationToken ct = default)
    {
        if (sourceNodeId == targetNodeId)
            throw new InvalidOperationException("Cannot connect a node to itself.");

        var edge = WorkflowEdge.Create(workflowId, sourceNodeId, sourcePort, targetNodeId, targetPort);
        await _repository.AddEdgeAsync(edge, ct);
        return ToEdgeDto(edge);
    }

    public async Task RemoveEdgeAsync(Guid workflowId, Guid edgeId, CancellationToken ct = default)
    {
        await _repository.RemoveEdgeAsync(edgeId, ct);
    }

    // --- Execution ---

    public async Task<Guid> StartRunAsync(Guid workflowId, string? inputsJson = null, CancellationToken ct = default)
    {
        var workflow = await _repository.GetByIdAsync(workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found.");

        // Validate graph before starting
        workflow.GetTopologicalOrder();

        var run = WorkflowRun.Create(workflowId, inputsJson);
        await _repository.AddRunAsync(run, ct);

        var jobData = JsonSerializer.Serialize(new { WorkflowRunId = run.Id });
        await _jobQueue.EnqueueAsync("workflow-run", jobData, ct);

        _logger.LogInformation("Workflow run {RunId} started for workflow {WorkflowId}", run.Id, workflowId);
        return run.Id;
    }

    public async Task<WorkflowRunDto?> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _repository.GetRunByIdAsync(runId, ct);
        return run is null ? null : ToRunDto(run);
    }

    // --- Import/Export ---

    public async Task<WorkflowExportFormat> ExportAsync(Guid workflowId, CancellationToken ct = default)
    {
        var workflow = await _repository.GetByIdAsync(workflowId, ct)
            ?? throw new InvalidOperationException($"Workflow {workflowId} not found.");

        var idMap = workflow.Nodes.ToDictionary(n => n.Id, n => n.Id.ToString());

        return new WorkflowExportFormat
        {
            Name = workflow.Name,
            Description = workflow.Description,
            CanvasStateJson = workflow.CanvasStateJson,
            Nodes = workflow.Nodes.Select(n => new ExportedNode(
                idMap[n.Id], n.PluginId, n.Label, n.PositionX, n.PositionY,
                n.ParametersJson, n.ConfigJson, n.MaxIterations)).ToList(),
            Edges = workflow.Edges.Select(e => new ExportedEdge(
                idMap[e.SourceNodeId], e.SourcePort, idMap[e.TargetNodeId], e.TargetPort)).ToList()
        };
    }

    public async Task<WorkflowDto> ImportAsync(WorkflowExportFormat data, CancellationToken ct = default)
    {
        // Create workflow with all nodes and edges in one shot (single AddAsync)
        var workflow = Workflow.Create(data.Name, data.Description);

        var idMap = new Dictionary<string, Guid>();

        foreach (var exportedNode in data.Nodes)
        {
            var node = workflow.AddNode(exportedNode.PluginId, exportedNode.Label,
                exportedNode.PositionX, exportedNode.PositionY,
                exportedNode.ParametersJson, exportedNode.ConfigJson);
            if (exportedNode.MaxIterations.HasValue)
                node.UpdateConfig(node.Label, node.ParametersJson, node.ConfigJson, exportedNode.MaxIterations);
            idMap[exportedNode.LocalId] = node.Id;
        }

        foreach (var exportedEdge in data.Edges)
        {
            if (idMap.TryGetValue(exportedEdge.SourceLocalId, out var sourceId) &&
                idMap.TryGetValue(exportedEdge.TargetLocalId, out var targetId))
            {
                workflow.AddEdge(sourceId, exportedEdge.SourcePort, targetId, exportedEdge.TargetPort);
            }
        }

        // Single save — workflow + all nodes + all edges in one transaction
        await _repository.AddAsync(workflow, ct);
        _logger.LogInformation("Imported workflow '{Name}' as {Id}", data.Name, workflow.Id);
        return ToDto(workflow);
    }

    public async Task<WorkflowDto> DuplicateAsync(Guid workflowId, CancellationToken ct = default)
    {
        var exported = await ExportAsync(workflowId, ct);
        var importData = exported with { Name = $"{exported.Name} (copy)" };
        return await ImportAsync(importData, ct);
    }

    public IReadOnlyList<WorkflowNodePluginDto> GetAvailablePlugins()
    {
        return _plugins.Select(p => new WorkflowNodePluginDto(
            p.PluginId, p.DisplayName, p.Category, p.Description, p.Icon)).ToList();
    }

    private static WorkflowDto ToDto(Workflow w) => new(
        w.Id, w.Name, w.Description, w.IsTemplate, w.CreatedAt, w.UpdatedAt,
        w.Nodes.Select(ToNodeDto).ToList(),
        w.Edges.Select(ToEdgeDto).ToList());

    private static WorkflowNodeDto ToNodeDto(WorkflowNode n) => new(
        n.Id, n.PluginId, n.Label, n.PositionX, n.PositionY,
        n.ParametersJson, n.ConfigJson, n.MaxIterations);

    private static WorkflowEdgeDto ToEdgeDto(WorkflowEdge e) => new(
        e.Id, e.SourceNodeId, e.SourcePort, e.TargetNodeId, e.TargetPort);

    private static WorkflowRunDto ToRunDto(WorkflowRun r) => new(
        r.Id, r.WorkflowId, r.Status, r.Error, r.CreatedAt, r.StartedAt, r.CompletedAt,
        r.Steps.Select(s => new WorkflowRunStepDto(
            s.Id, s.NodeId, s.Status, s.OutputImagePath, s.Error,
            s.Iteration, s.DurationMs, s.StartedAt, s.CompletedAt)).ToList());
}
