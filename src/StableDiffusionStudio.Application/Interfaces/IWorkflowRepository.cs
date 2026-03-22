using StableDiffusionStudio.Domain.Entities;

namespace StableDiffusionStudio.Application.Interfaces;

public interface IWorkflowRepository
{
    Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Workflow?> GetByIdWithRunsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Workflow>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Workflow workflow, CancellationToken ct = default);
    Task UpdateAsync(Workflow workflow, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Direct child entity operations (bypass parent entity tracking)
    Task AddNodeAsync(WorkflowNode node, CancellationToken ct = default);
    Task UpdateNodeAsync(WorkflowNode node, CancellationToken ct = default);
    Task RemoveNodeAsync(Guid nodeId, CancellationToken ct = default);
    Task AddEdgeAsync(WorkflowEdge edge, CancellationToken ct = default);
    Task RemoveEdgeAsync(Guid edgeId, CancellationToken ct = default);

    Task<WorkflowRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task AddRunAsync(WorkflowRun run, CancellationToken ct = default);
    Task UpdateRunAsync(WorkflowRun run, CancellationToken ct = default);
}
