using Microsoft.AspNetCore.SignalR;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Web.Hubs;

public class SignalRWorkflowNotifier : IWorkflowNotifier
{
    private readonly IHubContext<StudioHub> _hubContext;

    public SignalRWorkflowNotifier(IHubContext<StudioHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendStepStartedAsync(string workflowId, string runId, string nodeId, string nodeLabel)
    {
        await _hubContext.Clients.All.SendAsync("WorkflowStepStarted", workflowId, runId, nodeId, nodeLabel);
    }

    public async Task SendStepProgressAsync(string workflowId, string runId, string nodeId, int step, int totalSteps, string phase)
    {
        await _hubContext.Clients.All.SendAsync("WorkflowStepProgress", workflowId, runId, nodeId, step, totalSteps, phase);
    }

    public async Task SendStepCompletedAsync(string workflowId, string runId, string nodeId, string? outputImageUrl, long durationMs)
    {
        await _hubContext.Clients.All.SendAsync("WorkflowStepCompleted", workflowId, runId, nodeId, outputImageUrl, durationMs);
    }

    public async Task SendStepFailedAsync(string workflowId, string runId, string nodeId, string error)
    {
        await _hubContext.Clients.All.SendAsync("WorkflowStepFailed", workflowId, runId, nodeId, error);
    }

    public async Task SendRunCompletedAsync(string workflowId, string runId)
    {
        await _hubContext.Clients.All.SendAsync("WorkflowRunCompleted", workflowId, runId);
    }

    public async Task SendRunFailedAsync(string workflowId, string runId, string error)
    {
        await _hubContext.Clients.All.SendAsync("WorkflowRunFailed", workflowId, runId, error);
    }
}
