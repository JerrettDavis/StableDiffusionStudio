namespace StableDiffusionStudio.Domain.Entities;

public class Workflow
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? CanvasStateJson { get; private set; }
    public bool IsTemplate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<WorkflowNode> _nodes = [];
    public IReadOnlyList<WorkflowNode> Nodes => _nodes.AsReadOnly();

    private readonly List<WorkflowEdge> _edges = [];
    public IReadOnlyList<WorkflowEdge> Edges => _edges.AsReadOnly();

    private readonly List<WorkflowRun> _runs = [];
    public IReadOnlyList<WorkflowRun> Runs => _runs.AsReadOnly();

    private Workflow() { } // EF Core

    public static Workflow Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public WorkflowNode AddNode(string pluginId, string label, double positionX, double positionY,
        string? parametersJson = null, string? configJson = null)
    {
        var node = WorkflowNode.Create(Id, pluginId, label, positionX, positionY, parametersJson, configJson);
        _nodes.Add(node);
        UpdatedAt = DateTimeOffset.UtcNow;
        return node;
    }

    public void RemoveNode(Guid nodeId)
    {
        _nodes.RemoveAll(n => n.Id == nodeId);
        _edges.RemoveAll(e => e.SourceNodeId == nodeId || e.TargetNodeId == nodeId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public WorkflowEdge AddEdge(Guid sourceNodeId, string sourcePort, Guid targetNodeId, string targetPort)
    {
        if (sourceNodeId == targetNodeId)
            throw new InvalidOperationException("Cannot connect a node to itself.");

        var duplicate = _edges.Any(e =>
            e.TargetNodeId == targetNodeId && e.TargetPort == targetPort);
        if (duplicate)
            throw new InvalidOperationException($"Input port '{targetPort}' is already connected.");

        var edge = WorkflowEdge.Create(Id, sourceNodeId, sourcePort, targetNodeId, targetPort);
        _edges.Add(edge);
        UpdatedAt = DateTimeOffset.UtcNow;
        return edge;
    }

    public void RemoveEdge(Guid edgeId)
    {
        _edges.RemoveAll(e => e.Id == edgeId);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public WorkflowRun CreateRun(string? inputsJson = null)
    {
        var run = WorkflowRun.Create(Id, inputsJson);
        _runs.Add(run);
        return run;
    }

    public void Update(string name, string? description, string? canvasStateJson)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name is required.", nameof(name));
        Name = name.Trim();
        Description = description;
        CanvasStateJson = canvasStateJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetTemplate(bool isTemplate)
    {
        IsTemplate = isTemplate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Returns nodes in topological order (respecting edge dependencies).
    /// Throws if the graph contains a cycle (excluding intentional loops via MaxIterations).
    /// </summary>
    public IReadOnlyList<WorkflowNode> GetTopologicalOrder()
    {
        var inDegree = _nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = _nodes.ToDictionary(n => n.Id, _ => new List<Guid>());

        foreach (var edge in _edges)
        {
            // Skip edges that form intentional loops (target node has MaxIterations set)
            var targetNode = _nodes.FirstOrDefault(n => n.Id == edge.TargetNodeId);
            if (targetNode?.MaxIterations > 0)
            {
                var isBackEdge = HasPath(edge.TargetNodeId, edge.SourceNodeId);
                if (isBackEdge) continue;
            }

            inDegree[edge.TargetNodeId]++;
            adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
        }

        var queue = new Queue<Guid>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<WorkflowNode>();

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            sorted.Add(_nodes.First(n => n.Id == nodeId));
            foreach (var downstream in adjacency[nodeId])
            {
                inDegree[downstream]--;
                if (inDegree[downstream] == 0)
                    queue.Enqueue(downstream);
            }
        }

        if (sorted.Count != _nodes.Count)
            throw new InvalidOperationException("Workflow graph contains a cycle.");

        return sorted;
    }

    private bool HasPath(Guid from, Guid to)
    {
        var visited = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == to) return true;
            if (!visited.Add(current)) continue;
            foreach (var edge in _edges.Where(e => e.SourceNodeId == current))
                stack.Push(edge.TargetNodeId);
        }
        return false;
    }
}
