using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class WorkflowRun
{
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public WorkflowRunStatus Status { get; private set; }
    public string? InputsJson { get; private set; }
    public string? Error { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private readonly List<WorkflowRunStep> _steps = [];
    public IReadOnlyList<WorkflowRunStep> Steps => _steps.AsReadOnly();

    private WorkflowRun() { } // EF Core

    public static WorkflowRun Create(Guid workflowId, string? inputsJson = null)
    {
        return new WorkflowRun
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Status = WorkflowRunStatus.Pending,
            InputsJson = inputsJson,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Start()
    {
        Status = WorkflowRunStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void Complete()
    {
        Status = WorkflowRunStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string error)
    {
        Status = WorkflowRunStatus.Failed;
        Error = error;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = WorkflowRunStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public WorkflowRunStep AddStep(Guid nodeId)
    {
        var step = WorkflowRunStep.Create(Id, nodeId);
        _steps.Add(step);
        return step;
    }
}
