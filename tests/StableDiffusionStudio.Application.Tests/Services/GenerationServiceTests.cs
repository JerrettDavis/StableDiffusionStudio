using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Tests.Services;

public class GenerationServiceTests
{
    private readonly IGenerationJobRepository _jobRepo = Substitute.For<IGenerationJobRepository>();
    private readonly IModelCatalogRepository _modelCatalog = Substitute.For<IModelCatalogRepository>();
    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();
    private readonly GenerationService _service;

    private static readonly Guid CheckpointId = Guid.NewGuid();
    private static readonly Guid ProjectId = Guid.NewGuid();

    public GenerationServiceTests()
    {
        _service = new GenerationService(_jobRepo, _modelCatalog, _jobQueue);
    }

    private GenerationParameters ValidParameters => new()
    {
        PositivePrompt = "a beautiful landscape",
        CheckpointModelId = CheckpointId
    };

    private void SetupCheckpointExists()
    {
        var model = ModelRecord.Create("Test Checkpoint", "/models/test.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _modelCatalog.GetByIdAsync(CheckpointId, Arg.Any<CancellationToken>()).Returns(model);
    }

    [Fact]
    public async Task CreateAsync_WithValidCommand_CreatesJobAndEnqueues()
    {
        SetupCheckpointExists();
        var command = new CreateGenerationCommand(ProjectId, ValidParameters);

        var result = await _service.CreateAsync(command);

        result.Should().NotBeNull();
        result.ProjectId.Should().Be(ProjectId);
        result.Parameters.Should().Be(ValidParameters);
        result.Status.Should().Be(GenerationJobStatus.Pending);
        result.Images.Should().BeEmpty();
        await _jobRepo.Received(1).AddAsync(Arg.Any<GenerationJob>(), Arg.Any<CancellationToken>());
        await _jobQueue.Received(1).EnqueueAsync("generation", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithMissingCheckpoint_ThrowsKeyNotFoundException()
    {
        _modelCatalog.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ModelRecord?)null);
        var command = new CreateGenerationCommand(ProjectId, ValidParameters);

        var act = () => _service.CreateAsync(command);
        await act.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Checkpoint*");
    }

    [Fact]
    public async Task GetJobAsync_WhenExists_ReturnsDto()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        _jobRepo.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _service.GetJobAsync(job.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task GetJobAsync_WhenNotFound_ReturnsNull()
    {
        _jobRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GenerationJob?)null);

        var result = await _service.GetJobAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListJobsForProjectAsync_DelegatesToRepository()
    {
        var jobs = new List<GenerationJob>
        {
            GenerationJob.Create(ProjectId, ValidParameters),
            GenerationJob.Create(ProjectId, ValidParameters)
        };
        _jobRepo.ListByProjectAsync(ProjectId, 0, 20, Arg.Any<CancellationToken>()).Returns(jobs);

        var result = await _service.ListJobsForProjectAsync(ProjectId);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task CloneParametersAsync_WhenExists_ReturnsParameters()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        _jobRepo.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _service.CloneParametersAsync(job.Id);
        result.Should().Be(ValidParameters);
    }

    [Fact]
    public async Task CloneParametersAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        _jobRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GenerationJob?)null);

        var act = () => _service.CloneParametersAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetJobStatusAsync_WhenExists_ReturnsStatusDto()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        job.Start();
        _jobRepo.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _service.GetJobStatusAsync(job.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(GenerationJobStatus.Running);
        result.ElapsedSeconds.Should().NotBeNull();
    }

    [Fact]
    public async Task GetJobStatusAsync_WhenNotFound_ReturnsNull()
    {
        _jobRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GenerationJob?)null);

        var result = await _service.GetJobStatusAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetJobStatusAsync_CompletedJob_IncludesImageCount()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        job.Start();
        job.AddImage(GeneratedImage.Create(job.Id, "/img1.png", 42, 512, 512, 1.0, "{}"));
        job.AddImage(GeneratedImage.Create(job.Id, "/img2.png", 43, 512, 512, 1.0, "{}"));
        job.Complete();
        _jobRepo.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        var result = await _service.GetJobStatusAsync(job.Id);

        result!.ImageCount.Should().Be(2);
        result.Status.Should().Be(GenerationJobStatus.Completed);
    }

    [Fact]
    public async Task CancelGenerationAsync_PendingJob_MarksCancelled()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        _jobRepo.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        await _service.CancelGenerationAsync(job.Id);

        job.Status.Should().Be(GenerationJobStatus.Cancelled);
        await _jobRepo.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelGenerationAsync_RunningJob_MarksCancelled()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        job.Start();
        _jobRepo.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        await _service.CancelGenerationAsync(job.Id);

        job.Status.Should().Be(GenerationJobStatus.Cancelled);
        await _jobRepo.Received(1).UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelGenerationAsync_CompletedJob_DoesNotUpdate()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        job.Start();
        job.Complete();
        _jobRepo.GetByIdAsync(job.Id, Arg.Any<CancellationToken>()).Returns(job);

        await _service.CancelGenerationAsync(job.Id);

        job.Status.Should().Be(GenerationJobStatus.Completed);
        await _jobRepo.DidNotReceive().UpdateAsync(job, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelGenerationAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        _jobRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GenerationJob?)null);

        var act = () => _service.CancelGenerationAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ToggleFavoriteAsync_WhenExists_TogglesAndPersists()
    {
        var job = GenerationJob.Create(ProjectId, ValidParameters);
        var image = GeneratedImage.Create(job.Id, "/img.png", 42, 512, 512, 1.0, "{}");
        _jobRepo.GetImageByIdAsync(image.Id, Arg.Any<CancellationToken>()).Returns(image);

        await _service.ToggleFavoriteAsync(image.Id);

        image.IsFavorite.Should().BeTrue();
        await _jobRepo.Received(1).UpdateImageAsync(image, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleFavoriteAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        _jobRepo.GetImageByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GeneratedImage?)null);

        var act = () => _service.ToggleFavoriteAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
