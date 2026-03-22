using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;

namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Seeds starter workflow templates on first run.
/// Uses ImportAsync to create each template atomically (one DB save per template).
/// </summary>
public class WorkflowTemplateSeeder : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowTemplateSeeder> _logger;

    public WorkflowTemplateSeeder(IServiceScopeFactory scopeFactory, ILogger<WorkflowTemplateSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IWorkflowService>();

                var existing = await service.ListAsync(stoppingToken);
                if (existing.Count > 0)
                    return; // Templates or user workflows already exist

                _logger.LogInformation("Seeding starter workflow templates");

                await service.ImportAsync(BasicTemplate(), stoppingToken);
                await service.ImportAsync(UpscaleTemplate(), stoppingToken);
                await service.ImportAsync(RefineTemplate(), stoppingToken);

                _logger.LogInformation("Workflow templates seeded successfully");
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Workflow template seeding attempt {Attempt} failed", attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static WorkflowExportFormat BasicTemplate() => new()
    {
        Name = "Basic Generation",
        Description = "Simple txt2img to output pipeline",
        Nodes =
        [
            new("gen", "core.generate", "Generate", 100, 200, null, null, null),
            new("out", "core.output", "Output", 400, 200, null, null, null)
        ],
        Edges =
        [
            new("gen", "image", "out", "image")
        ]
    };

    private static WorkflowExportFormat UpscaleTemplate() => new()
    {
        Name = "Generate + Upscale",
        Description = "Generate an image then upscale it 2x with img2img",
        Nodes =
        [
            new("gen", "core.generate", "Base Generation", 100, 200, null, null, null),
            new("up", "core.upscale", "Upscale 2x", 400, 200, null, null, null),
            new("out", "core.output", "Output", 700, 200, null, null, null)
        ],
        Edges =
        [
            new("gen", "image", "up", "image"),
            new("up", "image", "out", "image")
        ]
    };

    private static WorkflowExportFormat RefineTemplate() => new()
    {
        Name = "Generate + Refine",
        Description = "Generate a base image, then refine details with a second img2img pass",
        Nodes =
        [
            new("gen", "core.generate", "Base Generation", 100, 200, null, null, null),
            new("ref", "core.img2img", "Refine Details", 400, 200, null, null, null),
            new("out", "core.output", "Output", 700, 200, null, null, null)
        ],
        Edges =
        [
            new("gen", "image", "ref", "image"),
            new("ref", "image", "out", "image")
        ]
    };
}
