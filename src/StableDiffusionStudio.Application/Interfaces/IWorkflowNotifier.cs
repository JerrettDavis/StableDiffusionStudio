namespace StableDiffusionStudio.Application.Interfaces;

public interface IWorkflowNotifier
{
    Task SendStepStartedAsync(string workflowId, string runId, string nodeId, string nodeLabel);
    Task SendStepProgressAsync(string workflowId, string runId, string nodeId, int step, int totalSteps, string phase);
    Task SendStepCompletedAsync(string workflowId, string runId, string nodeId, string? outputImageUrl, long durationMs);
    Task SendStepFailedAsync(string workflowId, string runId, string nodeId, string error);
    Task SendRunCompletedAsync(string workflowId, string runId);
    Task SendRunFailedAsync(string workflowId, string runId, string error);
}
