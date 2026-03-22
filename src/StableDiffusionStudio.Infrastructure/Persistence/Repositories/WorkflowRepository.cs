using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Persistence.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly AppDbContext _context;

    public WorkflowRepository(AppDbContext context) => _context = context;

    public async Task<Workflow?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Workflow?> GetByIdWithRunsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Workflows
            .Include(w => w.Nodes)
            .Include(w => w.Edges)
            .Include(w => w.Runs)
                .ThenInclude(r => r.Steps)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<IReadOnlyList<Workflow>> ListAsync(CancellationToken ct = default)
    {
        return await _context.Workflows
            .AsNoTracking()
            .OrderByDescending(w => w.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Workflow workflow, CancellationToken ct = default)
    {
        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync(ct);
        DetachAll(workflow);
    }

    public async Task UpdateAsync(Workflow workflow, CancellationToken ct = default)
    {
        _context.Workflows.Update(workflow);
        await _context.SaveChangesAsync(ct);
        DetachAll(workflow);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _context.Workflows.Where(w => w.Id == id).ExecuteDeleteAsync(ct);
    }

    // --- Direct child entity operations ---
    // These bypass the parent aggregate to avoid EF Core tracking/concurrency issues
    // when the parent was loaded in a different operation within the same scope.

    public async Task AddNodeAsync(WorkflowNode node, CancellationToken ct = default)
    {
        _context.WorkflowNodes.Add(node);
        await _context.SaveChangesAsync(ct);
        _context.Entry(node).State = EntityState.Detached;
    }

    public async Task UpdateNodeAsync(WorkflowNode node, CancellationToken ct = default)
    {
        _context.WorkflowNodes.Update(node);
        await _context.SaveChangesAsync(ct);
        _context.Entry(node).State = EntityState.Detached;
    }

    public async Task RemoveNodeAsync(Guid nodeId, CancellationToken ct = default)
    {
        await _context.WorkflowEdges
            .Where(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId)
            .ExecuteDeleteAsync(ct);
        await _context.WorkflowNodes.Where(n => n.Id == nodeId).ExecuteDeleteAsync(ct);
    }

    public async Task AddEdgeAsync(WorkflowEdge edge, CancellationToken ct = default)
    {
        _context.WorkflowEdges.Add(edge);
        await _context.SaveChangesAsync(ct);
        _context.Entry(edge).State = EntityState.Detached;
    }

    public async Task RemoveEdgeAsync(Guid edgeId, CancellationToken ct = default)
    {
        await _context.WorkflowEdges.Where(e => e.Id == edgeId).ExecuteDeleteAsync(ct);
    }

    public async Task<WorkflowRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default)
    {
        return await _context.WorkflowRuns
            .Include(r => r.Steps)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    public async Task AddRunAsync(WorkflowRun run, CancellationToken ct = default)
    {
        _context.WorkflowRuns.Add(run);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateRunAsync(WorkflowRun run, CancellationToken ct = default)
    {
        _context.WorkflowRuns.Update(run);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Detach a workflow and all its child entities from the change tracker
    /// to prevent stale tracking from causing duplicate key errors on subsequent queries.
    /// </summary>
    private void DetachAll(Workflow workflow)
    {
        foreach (var node in workflow.Nodes)
            _context.Entry(node).State = EntityState.Detached;
        foreach (var edge in workflow.Edges)
            _context.Entry(edge).State = EntityState.Detached;
        foreach (var run in workflow.Runs)
        {
            foreach (var step in run.Steps)
                _context.Entry(step).State = EntityState.Detached;
            _context.Entry(run).State = EntityState.Detached;
        }
        _context.Entry(workflow).State = EntityState.Detached;
    }
}
