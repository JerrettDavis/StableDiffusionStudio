using System.Text.Json;
using Microsoft.Extensions.Logging;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Services;

public class GenerationService
{
    private readonly IGenerationJobRepository _repository;
    private readonly IModelCatalogRepository _modelCatalog;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<GenerationService>? _logger;

    public GenerationService(
        IGenerationJobRepository repository,
        IModelCatalogRepository modelCatalog,
        IJobQueue jobQueue,
        ILogger<GenerationService>? logger = null)
    {
        _repository = repository;
        _modelCatalog = modelCatalog;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task<GenerationJobDto> CreateAsync(CreateGenerationCommand command, CancellationToken ct = default)
    {
        var checkpoint = await _modelCatalog.GetByIdAsync(command.Parameters.CheckpointModelId, ct);
        if (checkpoint is null)
            throw new KeyNotFoundException($"Checkpoint model {command.Parameters.CheckpointModelId} not found.");

        var job = GenerationJob.Create(command.ProjectId, command.Parameters);
        await _repository.AddAsync(job, ct);

        var jobData = JsonSerializer.Serialize(new { GenerationJobId = job.Id });
        await _jobQueue.EnqueueAsync("generation", jobData, ct);

        _logger?.LogInformation("Generation job {JobId} created for project {ProjectId}", job.Id, command.ProjectId);
        return ToDto(job);
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

    private static GeneratedImageDto ToImageDto(GeneratedImage i) =>
        new(i.Id, i.GenerationJobId, i.FilePath, i.Seed, i.Width, i.Height,
            i.GenerationTimeSeconds, i.ParametersJson, i.CreatedAt, i.IsFavorite);
}
