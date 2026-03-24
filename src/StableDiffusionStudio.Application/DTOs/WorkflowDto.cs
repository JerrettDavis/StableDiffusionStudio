using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.DTOs;

public record WorkflowDto(
    Guid Id, string Name, string? Description, bool IsTemplate,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowNodeDto> Nodes, IReadOnlyList<WorkflowEdgeDto> Edges);

public record WorkflowNodeDto(
    Guid Id, string PluginId, string Label,
    double PositionX, double PositionY,
    string? ParametersJson, string? ConfigJson, int? MaxIterations);

public record WorkflowEdgeDto(
    Guid Id, Guid SourceNodeId, string SourcePort, Guid TargetNodeId, string TargetPort);

public record WorkflowRunDto(
    Guid Id, Guid WorkflowId, WorkflowRunStatus Status,
    string? Error, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt,
    IReadOnlyList<WorkflowRunStepDto> Steps);

public record WorkflowRunStepDto(
    Guid Id, Guid NodeId, WorkflowStepStatus Status,
    string? OutputImagePath, string? Error, int Iteration,
    long DurationMs, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);

public record WorkflowListItemDto(Guid Id, string Name, string? Description, bool IsTemplate,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int NodeCount, int EdgeCount,
    Domain.Enums.WorkflowRunStatus? LastRunStatus, DateTimeOffset? LastRunAt);

public record WorkflowNodePluginDto(string PluginId, string DisplayName, string Category,
    string Description, string Icon);
