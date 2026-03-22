using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class WorkflowRunStep
{
    public Guid Id { get; private set; }
    public Guid WorkflowRunId { get; private set; }
    public Guid NodeId { get; private set; }
    public WorkflowStepStatus Status { get; private set; }
    public string? OutputImagePath { get; private set; }
    public string? OutputDataJson { get; private set; }
    public string? Error { get; private set; }
    public int Iteration { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public long DurationMs { get; private set; }

    private WorkflowRunStep() { } // EF Core

    public static WorkflowRunStep Create(Guid workflowRunId, Guid nodeId, int iteration = 0)
    {
        return new WorkflowRunStep
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = workflowRunId,
            NodeId = nodeId,
            Status = WorkflowStepStatus.Pending,
            Iteration = iteration
        };
    }

    public void Start()
    {
        Status = WorkflowStepStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void Complete(string? outputImagePath, string? outputDataJson, long durationMs)
    {
        Status = WorkflowStepStatus.Completed;
        OutputImagePath = outputImagePath;
        OutputDataJson = outputDataJson;
        DurationMs = durationMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string error, long durationMs)
    {
        Status = WorkflowStepStatus.Failed;
        Error = error;
        DurationMs = durationMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Skip()
    {
        Status = WorkflowStepStatus.Skipped;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
