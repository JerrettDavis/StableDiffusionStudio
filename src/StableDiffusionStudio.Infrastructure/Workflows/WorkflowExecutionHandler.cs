using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Jobs;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Workflows;

/// <summary>
/// Executes a workflow run by walking the node graph in topological order.
/// Each node is resolved to an IWorkflowNodePlugin and executed.
/// Optimizes model loading by grouping consecutive same-checkpoint nodes.
/// </summary>
public class WorkflowExecutionHandler : IJobHandler
{
    private readonly AppDbContext _context;
    private readonly IInferenceBackend _inferenceBackend;
    private readonly IModelCatalogRepository _modelCatalogRepository;
    private readonly IAppPaths _appPaths;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowNotifier _notifier;
    private readonly ILogger<WorkflowExecutionHandler> _logger;
    private Guid? _currentCheckpointId;

    public WorkflowExecutionHandler(
        AppDbContext context,
        IInferenceBackend inferenceBackend,
        IModelCatalogRepository modelCatalogRepository,
        IAppPaths appPaths,
        IServiceProvider serviceProvider,
        IWorkflowNotifier notifier,
        ILogger<WorkflowExecutionHandler> logger)
    {
        _context = context;
        _inferenceBackend = inferenceBackend;
        _modelCatalogRepository = modelCatalogRepository;
        _appPaths = appPaths;
        _serviceProvider = serviceProvider;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task HandleAsync(JobRecord job, CancellationToken ct)
    {
        var jobData = JsonSerializer.Deserialize<WorkflowRunJobData>(job.Data ?? "{}");
        if (jobData?.WorkflowRunId is null)
        {
            job.Fail("Invalid workflow run job data.");
            return;
        }

        var run = await _context.WorkflowRuns
            .FindAsync([jobData.WorkflowRunId], ct);
        if (run is null)
        {
            job.Fail($"Workflow run {jobData.WorkflowRunId} not found.");
            return;
        }

        // Load the workflow with nodes and edges
        var workflow = await _context.Workflows
            .FindAsync([run.WorkflowId], ct);
        if (workflow is null)
        {
            run.Fail("Workflow not found.");
            await _context.SaveChangesAsync(ct);
            return;
        }

        // Eagerly load nodes and edges
        await _context.Entry(workflow).Collection(w => w.Nodes).LoadAsync(ct);
        await _context.Entry(workflow).Collection(w => w.Edges).LoadAsync(ct);
        await _context.Entry(run).Collection(r => r.Steps).LoadAsync(ct);

        run.Start();
        await _context.SaveChangesAsync(ct);

        try
        {
            var sortedNodes = workflow.GetTopologicalOrder();
            var nodeOutputs = new Dictionary<Guid, Dictionary<string, WorkflowData>>();

            // Parse entry-point inputs if provided
            var entryInputs = !string.IsNullOrWhiteSpace(run.InputsJson)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(run.InputsJson) ?? []
                : new Dictionary<string, string>();

            // Resolve all plugins upfront
            var plugins = _serviceProvider.GetServices<IWorkflowNodePlugin>()
                .ToDictionary(p => p.PluginId);

            _currentCheckpointId = null;

            foreach (var node in sortedNodes)
            {
                ct.ThrowIfCancellationRequested();

                if (!plugins.TryGetValue(node.PluginId, out var plugin))
                {
                    _logger.LogError("No plugin found for {PluginId}", node.PluginId);
                    throw new InvalidOperationException($"Unknown plugin: {node.PluginId}");
                }

                // Collect inputs from upstream edges
                var inputs = new Dictionary<string, WorkflowData>();
                foreach (var edge in workflow.Edges.Where(e => e.TargetNodeId == node.Id))
                {
                    if (nodeOutputs.TryGetValue(edge.SourceNodeId, out var sourceOutputs) &&
                        sourceOutputs.TryGetValue(edge.SourcePort, out var data))
                    {
                        inputs[edge.TargetPort] = data;
                    }
                }

                // Inject entry-point images for nodes with unconnected required image inputs
                if (!inputs.ContainsKey("image") &&
                    plugin.InputPorts.Any(p => p.Name == "image" && p.Required) &&
                    entryInputs.TryGetValue(node.Id.ToString(), out var base64Image))
                {
                    inputs["image"] = WorkflowData.FromImage(Convert.FromBase64String(base64Image));
                }

                // Load model if this is a generation node with a different checkpoint
                await LoadModelIfNeeded(node, ct);

                // Create run step and notify
                var step = run.AddStep(node.Id);
                step.Start();
                await _context.SaveChangesAsync(ct);
                await NotifySafe(() => _notifier.SendStepStartedAsync(
                    workflow.Id.ToString(), run.Id.ToString(), node.Id.ToString(), node.Label));

                var sw = Stopwatch.StartNew();
                var wfId = workflow.Id.ToString();
                var rId = run.Id.ToString();
                var nId = node.Id.ToString();

                try
                {
                    var executionContext = new WorkflowExecutionContext
                    {
                        InferenceBackend = _inferenceBackend,
                        ModelRepository = _modelCatalogRepository,
                        AppPaths = _appPaths,
                        WorkflowRunId = run.Id,
                        NodeId = node.Id,
                        Progress = new Progress<WorkflowStepProgress>(p =>
                        {
                            _ = NotifySafe(() => _notifier.SendStepProgressAsync(
                                wfId, rId, nId, p.CurrentStep, p.TotalSteps, p.Phase ?? "Processing"));
                        })
                    };

                    var outputs = await plugin.ExecuteAsync(inputs, node.ParametersJson ?? node.ConfigJson, executionContext, ct);
                    nodeOutputs[node.Id] = outputs;

                    sw.Stop();
                    var outputImagePath = outputs.TryGetValue("image", out var img) && img.ImageBytes is not null
                        ? Path.Combine(_appPaths.AssetsDirectory, "workflows", run.Id.ToString(), $"{node.Id}.png")
                        : null;

                    step.Complete(outputImagePath, null, sw.ElapsedMilliseconds);
                    _logger.LogInformation("Node {Label} ({PluginId}) completed in {Ms}ms",
                        node.Label, node.PluginId, sw.ElapsedMilliseconds);

                    var imageUrl = outputImagePath is not null ? _appPaths.GetImageUrl(outputImagePath) : null;
                    await NotifySafe(() => _notifier.SendStepCompletedAsync(
                        wfId, rId, nId, imageUrl, sw.ElapsedMilliseconds));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    step.Fail(ex.Message, sw.ElapsedMilliseconds);
                    await NotifySafe(() => _notifier.SendStepFailedAsync(wfId, rId, nId, ex.Message));
                    throw;
                }

                await _context.SaveChangesAsync(ct);
            }

            run.Complete();
            _logger.LogInformation("Workflow run {RunId} completed successfully", run.Id);
            await NotifySafe(() => _notifier.SendRunCompletedAsync(workflow.Id.ToString(), run.Id.ToString()));
        }
        catch (OperationCanceledException)
        {
            run.Cancel();
            _logger.LogInformation("Workflow run {RunId} cancelled", run.Id);
        }
        catch (Exception ex)
        {
            run.Fail(ex.Message);
            _logger.LogError(ex, "Workflow run {RunId} failed", run.Id);
            await NotifySafe(() => _notifier.SendRunFailedAsync(workflow.Id.ToString(), run.Id.ToString(), ex.Message));
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task LoadModelIfNeeded(WorkflowNode node, CancellationToken ct)
    {
        // Only generation-related plugins need model loading
        if (node.PluginId is not ("core.generate" or "core.img2img" or "core.inpaint" or "core.controlnet" or "core.upscale"))
            return;

        if (string.IsNullOrWhiteSpace(node.ParametersJson))
            return;

        GenerationParameters? parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<GenerationParameters>(node.ParametersJson);
        }
        catch
        {
            return;
        }

        if (parameters is null) return;

        // Skip if same checkpoint is already loaded
        if (_currentCheckpointId == parameters.CheckpointModelId)
            return;

        var checkpoint = await _modelCatalogRepository.GetByIdAsync(parameters.CheckpointModelId, ct);
        if (checkpoint is null)
            throw new InvalidOperationException($"Checkpoint model {parameters.CheckpointModelId} not found.");

        // Resolve VAE
        string? vaePath = null;
        if (parameters.VaeModelId.HasValue)
        {
            var vae = await _modelCatalogRepository.GetByIdAsync(parameters.VaeModelId.Value, ct);
            vaePath = vae?.FilePath;
        }

        // Resolve LoRAs
        var loras = new List<LoraLoadInfo>();
        foreach (var loraRef in parameters.Loras)
        {
            var loraModel = await _modelCatalogRepository.GetByIdAsync(loraRef.ModelId, ct);
            if (loraModel is not null)
                loras.Add(new LoraLoadInfo(loraModel.FilePath, loraRef.Weight));
        }

        _logger.LogInformation("Loading model for workflow node {Label}: {Path}",
            node.Label, checkpoint.FilePath);

        await _inferenceBackend.LoadModelAsync(
            new ModelLoadRequest(checkpoint.FilePath, vaePath, loras), ct);

        _currentCheckpointId = parameters.CheckpointModelId;
    }

    private async Task NotifySafe(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { _logger.LogDebug(ex, "Workflow notification failed (non-critical)"); }
    }

    private record WorkflowRunJobData(Guid? WorkflowRunId);
}
