using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Mcp.Tools;

[McpServerToolType]
public class WorkflowTools
{
    [McpServerTool(Name = "list_workflows"), Description(
        "List all saved workflows with their names and descriptions.")]
    public static async Task<string> ListWorkflows(
        IWorkflowService workflowService)
    {
        var workflows = await workflowService.ListAsync();
        return JsonSerializer.Serialize(new
        {
            workflows = workflows.Select(w => new
            {
                id = w.Id,
                name = w.Name,
                description = w.Description,
                isTemplate = w.IsTemplate,
                updatedAt = w.UpdatedAt
            }),
            count = workflows.Count
        });
    }

    [McpServerTool(Name = "run_workflow"), Description(
        "Execute a saved workflow. Returns a run ID to track progress with get_workflow_run_status.")]
    public static async Task<string> RunWorkflow(
        IWorkflowService workflowService,
        [Description("Workflow GUID from list_workflows")] string workflowId,
        [Description("JSON map of node ID → base64 image for entry-point nodes (optional)")] string? inputsJson = null)
    {
        if (!Guid.TryParse(workflowId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid workflow ID" });

        try
        {
            var runId = await workflowService.StartRunAsync(id, inputsJson);
            return JsonSerializer.Serialize(new
            {
                runId,
                workflowId = id,
                message = $"Workflow run started. Poll with get_workflow_run_status(runId: \"{runId}\")"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [McpServerTool(Name = "get_workflow_run_status"), Description(
        "Check the status of a workflow run. Returns per-node step status and output images.")]
    public static async Task<string> GetWorkflowRunStatus(
        IWorkflowService workflowService,
        [Description("Run GUID from run_workflow")] string runId)
    {
        if (!Guid.TryParse(runId, out var id))
            return JsonSerializer.Serialize(new { error = "Invalid run ID" });

        var run = await workflowService.GetRunAsync(id);
        if (run is null)
            return JsonSerializer.Serialize(new { error = "Run not found" });

        return JsonSerializer.Serialize(new
        {
            runId = run.Id,
            workflowId = run.WorkflowId,
            status = run.Status.ToString(),
            error = run.Error,
            startedAt = run.StartedAt,
            completedAt = run.CompletedAt,
            steps = run.Steps.Select(s => new
            {
                nodeId = s.NodeId,
                status = s.Status.ToString(),
                outputImagePath = s.OutputImagePath,
                error = s.Error,
                durationMs = s.DurationMs,
                iteration = s.Iteration
            })
        });
    }
}
