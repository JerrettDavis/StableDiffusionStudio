using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Services;

public class GenerationService : IGenerationService
{
    private readonly IGenerationJobRepository _repository;
    private readonly IModelCatalogRepository _modelCatalog;
    private readonly IJobQueue _jobQueue;
    private readonly IAppPaths _appPaths;
    private readonly ILogger<GenerationService>? _logger;

    public GenerationService(
        IGenerationJobRepository repository,
        IModelCatalogRepository modelCatalog,
        IJobQueue jobQueue,
        IAppPaths appPaths,
        ILogger<GenerationService>? logger = null)
    {
        _repository = repository;
        _modelCatalog = modelCatalog;
        _jobQueue = jobQueue;
        _appPaths = appPaths;
        _logger = logger;
    }

    public async Task<GenerationJobDto> CreateAsync(CreateGenerationCommand command, CancellationToken ct = default)
    {
        var checkpoint = await _modelCatalog.GetByIdAsync(command.Parameters.CheckpointModelId, ct);
        if (checkpoint is null)
            throw new KeyNotFoundException($"Checkpoint model {command.Parameters.CheckpointModelId} not found.");

        var parameters = command.Parameters;

        // If init image bytes are provided, save them to disk and set the path on parameters
        if (command.InitImageBytes is not null)
        {
            var initDir = _appPaths.GetProjectAssetsDirectory(command.ProjectId);
            Directory.CreateDirectory(initDir);
            var initPath = Path.Combine(initDir, $"init_{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(initPath, command.InitImageBytes, ct);
            parameters = parameters with { InitImagePath = initPath };
        }

        // If mask image bytes are provided, save them to disk and set the path on parameters
        if (command.MaskImageBytes is not null)
        {
            var maskDir = _appPaths.GetProjectAssetsDirectory(command.ProjectId);
            Directory.CreateDirectory(maskDir);
            var maskPath = Path.Combine(maskDir, $"mask_{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(maskPath, command.MaskImageBytes, ct);
            parameters = parameters with { MaskImagePath = maskPath };
        }

        var job = GenerationJob.Create(command.ProjectId, parameters);
        await _repository.AddAsync(job, ct);

        var jobData = JsonSerializer.Serialize(new { GenerationJobId = job.Id });
        await _jobQueue.EnqueueAsync("generation", jobData, ct);

        _logger?.LogInformation("Generation job {JobId} created for project {ProjectId}", job.Id, command.ProjectId);
        return ToDto(job);
    }

    public async Task<IReadOnlyList<GenerationJobDto>> CreateWithMatrixAsync(CreateGenerationCommand command, CancellationToken ct = default)
    {
        var prompts = PromptMatrixParser.ExpandMatrix(command.Parameters.PositivePrompt);
        if (prompts.Count <= 1)
        {
            var single = await CreateAsync(command, ct);
            return [single];
        }

        var results = new List<GenerationJobDto>();
        foreach (var prompt in prompts)
        {
            var variantParams = command.Parameters with { PositivePrompt = prompt };
            var variantCommand = new CreateGenerationCommand(command.ProjectId, variantParams, command.InitImageBytes);
            var job = await CreateAsync(variantCommand, ct);
            results.Add(job);
        }

        _logger?.LogInformation("Prompt matrix expanded into {Count} jobs for project {ProjectId}",
            results.Count, command.ProjectId);
        return results;
    }

    public async Task<GenerationJobDto?> GetJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(id, ct);
        return job is null ? null : ToDto(job);
    }

    public async Task<IReadOnlyList<GenerationJobDto>> ListJobsForProjectAsync(
        Guid projectId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        var jobs = await _repository.ListByProjectAsync(projectId, skip, take, ct);
        return jobs.Select(ToDto).ToList();
    }

    public async Task<GenerationStatusDto?> GetJobStatusAsync(Guid generationJobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(generationJobId, ct);
        if (job is null) return null;

        double? elapsed = job.StartedAt.HasValue
            ? (job.CompletedAt ?? DateTimeOffset.UtcNow).Subtract(job.StartedAt.Value).TotalSeconds
            : null;

        return new GenerationStatusDto(
            job.Status,
            0,
            null,
            job.ErrorMessage,
            job.Images.Count,
            elapsed);
    }

    public async Task<GenerationParameters> CloneParametersAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(jobId, ct);
        if (job is null)
            throw new KeyNotFoundException($"Generation job {jobId} not found.");

        return job.Parameters;
    }

    private static GenerationJobDto ToDto(GenerationJob j) =>
        new(j.Id, j.ProjectId, j.Parameters, j.Status, j.CreatedAt, j.StartedAt, j.CompletedAt,
            j.ErrorMessage, j.Images.Select(ToImageDto).ToList());

    public async Task ToggleFavoriteAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _repository.GetImageByIdAsync(imageId, ct);
        if (image is null)
            throw new KeyNotFoundException($"Image {imageId} not found.");

        image.ToggleFavorite();
        await _repository.UpdateImageAsync(image, ct);
        _logger?.LogInformation("Toggled favorite for image {ImageId}, now {IsFavorite}", imageId, image.IsFavorite);
    }

    public async Task RevealImageAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _repository.GetImageByIdAsync(imageId, ct);
        if (image is null)
            throw new KeyNotFoundException($"Image {imageId} not found.");

        image.Reveal();
        await _repository.UpdateImageAsync(image, ct);
    }

    public async Task ConcealImageAsync(Guid imageId, CancellationToken ct = default)
    {
        var image = await _repository.GetImageByIdAsync(imageId, ct);
        if (image is null)
            throw new KeyNotFoundException($"Image {imageId} not found.");

        image.Conceal();
        await _repository.UpdateImageAsync(image, ct);
    }

    public async Task CancelGenerationAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(jobId, ct);
        if (job is null)
            throw new KeyNotFoundException($"Generation job {jobId} not found.");

        if (job.Status is GenerationJobStatus.Pending or GenerationJobStatus.Running)
        {
            job.Cancel();
            await _repository.UpdateAsync(job, ct);
            _logger?.LogInformation("Cancelled generation job {JobId}", jobId);
        }
    }

    private static GeneratedImageDto ToImageDto(GeneratedImage i) =>
        new(i.Id, i.GenerationJobId, i.FilePath, i.Seed, i.Width, i.Height,
            i.GenerationTimeSeconds, i.ParametersJson, i.CreatedAt, i.IsFavorite,
            i.ContentRating, i.NsfwScore, i.IsRevealed);
}
