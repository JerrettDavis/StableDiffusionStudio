namespace StableDiffusionStudio.Domain.Entities;

public class WorkflowEdge
{
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public Guid SourceNodeId { get; private set; }
    public string SourcePort { get; private set; } = string.Empty;
    public Guid TargetNodeId { get; private set; }
    public string TargetPort { get; private set; } = string.Empty;

    private WorkflowEdge() { } // EF Core

    public static WorkflowEdge Create(Guid workflowId, Guid sourceNodeId, string sourcePort,
        Guid targetNodeId, string targetPort)
    {
        if (string.IsNullOrWhiteSpace(sourcePort))
            throw new ArgumentException("Source port is required.", nameof(sourcePort));
        if (string.IsNullOrWhiteSpace(targetPort))
            throw new ArgumentException("Target port is required.", nameof(targetPort));

        return new WorkflowEdge
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            SourceNodeId = sourceNodeId,
            SourcePort = sourcePort,
            TargetNodeId = targetNodeId,
            TargetPort = targetPort
        };
    }
}
