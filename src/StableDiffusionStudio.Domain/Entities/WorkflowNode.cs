namespace StableDiffusionStudio.Domain.Entities;

public class WorkflowNode
{
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public string PluginId { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public double PositionX { get; private set; }
    public double PositionY { get; private set; }
    public string? ParametersJson { get; private set; }
    public string? ConfigJson { get; private set; }
    public int? MaxIterations { get; private set; }

    private WorkflowNode() { } // EF Core

    public static WorkflowNode Create(Guid workflowId, string pluginId, string label,
        double positionX, double positionY, string? parametersJson = null, string? configJson = null)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("Plugin ID is required.", nameof(pluginId));

        return new WorkflowNode
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            PluginId = pluginId,
            Label = string.IsNullOrWhiteSpace(label) ? pluginId : label.Trim(),
            PositionX = positionX,
            PositionY = positionY,
            ParametersJson = parametersJson,
            ConfigJson = configJson
        };
    }

    public void UpdatePosition(double x, double y)
    {
        PositionX = x;
        PositionY = y;
    }

    public void UpdateConfig(string label, string? parametersJson, string? configJson, int? maxIterations = null)
    {
        Label = string.IsNullOrWhiteSpace(label) ? PluginId : label.Trim();
        ParametersJson = parametersJson;
        ConfigJson = configJson;
        MaxIterations = maxIterations;
    }
}
