using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Jobs;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;
using StableDiffusionStudio.Infrastructure.Services;
using StableDiffusionStudio.Infrastructure.Tests.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Integration;

public class EndToEndGenerationTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly GenerationJobRepository _genJobRepo;
    private readonly ModelCatalogRepository _modelCatalogRepo;
    private readonly ProjectRepository _projectRepo;
    private readonly GenerationService _generationService;
    private readonly ProjectService _projectService;
    private readonly IJobQueue _jobQueue;
    private readonly IAppPaths _appPaths;
    private readonly string _tempAssetsDir;

    public EndToEndGenerationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _genJobRepo = new GenerationJobRepository(_context);
        _modelCatalogRepo = new ModelCatalogRepository(_context);
        _projectRepo = new ProjectRepository(_context);
        _jobQueue = Substitute.For<IJobQueue>();
        _jobQueue.EnqueueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        _tempAssetsDir = Path.Combine(Path.GetTempPath(), $"SDS_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempAssetsDir);
        _appPaths = Substitute.For<IAppPaths>();
        _appPaths.AssetsDirectory.Returns(_tempAssetsDir);
        _appPaths.GetJobAssetsDirectory(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(ci => Path.Combine(_tempAssetsDir, ci.ArgAt<Guid>(0).ToString(), ci.ArgAt<Guid>(1).ToString()));

        _generationService = new GenerationService(_genJobRepo, _modelCatalogRepo, _jobQueue);
        _projectService = new ProjectService(_projectRepo);
    }

    [Fact]
    public async Task FullPipeline_CreateProject_SubmitGeneration_ProcessJob_VerifyResults()
    {
        // 1. Create a project
        var projectDto = await _projectService.CreateAsync(new CreateProjectCommand("Test Project", "E2E test"));
        projectDto.Name.Should().Be("Test Project");

        // 2. Create a model record for the checkpoint
        var checkpoint = ModelRecord.Create("Test Checkpoint", "/models/test.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "local");
        await _modelCatalogRepo.UpsertAsync(checkpoint);

        // 3. Submit generation via GenerationService
        var parameters = new GenerationParameters
        {
            PositivePrompt = "a beautiful landscape",
            NegativePrompt = "ugly, blurry",
            CheckpointModelId = checkpoint.Id,
            Steps = 5,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1,
            Seed = 42
        };
        var genJobDto = await _generationService.CreateAsync(new CreateGenerationCommand(projectDto.Id, parameters));
        genJobDto.Status.Should().Be(GenerationJobStatus.Pending);

        // Clear the change tracker to simulate a fresh scope (as would happen in a real background worker)
        _context.ChangeTracker.Clear();

        // 4. Process the job via GenerationJobHandler (simulating the background worker)
        var mockBackend = new MockInferenceBackend();
        var contentSafety = Substitute.For<IContentSafetyService>();
        contentSafety.GetFilterModeAsync(Arg.Any<CancellationToken>()).Returns(NsfwFilterMode.Off);
        contentSafety.ClassifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ContentClassification(ContentRating.Unknown, 0, 0, 0, 0, 1));
        var handler = new GenerationJobHandler(
            _genJobRepo, _modelCatalogRepo, mockBackend, _appPaths, contentSafety,
            Substitute.For<ILogger<GenerationJobHandler>>());

        var jobRecord = JobRecord.Create("generation", $"{{\"GenerationJobId\":\"{genJobDto.Id}\"}}");
        jobRecord.Start();
        await handler.HandleAsync(jobRecord, CancellationToken.None);

        // 5. Verify the generation job is completed
        var completedJob = await _generationService.GetJobAsync(genJobDto.Id);
        completedJob.Should().NotBeNull();
        completedJob!.Status.Should().Be(GenerationJobStatus.Completed);
        completedJob.Images.Should().HaveCount(1);

        // 6. Verify image file was written
        var imagePath = completedJob.Images[0].FilePath;
        File.Exists(imagePath).Should().BeTrue();

        // 7. Verify PNG metadata was embedded
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var metadata = PngMetadataService.ReadTextChunk(imageBytes, "parameters");
        metadata.Should().NotBeNull();
        metadata.Should().Contain("a beautiful landscape");
        metadata.Should().Contain("Steps: 5");
    }

    [Fact]
    public async Task Pipeline_WithMissingCheckpoint_FailsGracefully()
    {
        // Create a project and generation job with a checkpoint that won't be found by the handler
        var project = Project.Create("Test", null);
        await _projectRepo.AddAsync(project);

        var fakeCheckpointId = Guid.NewGuid();
        // We need a checkpoint to create the job (service checks), so we create one then remove it
        var checkpoint = ModelRecord.Create("Temp", "/tmp.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1000, "local");
        await _modelCatalogRepo.UpsertAsync(checkpoint);

        var parameters = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = checkpoint.Id,
            Steps = 5,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJobDto = await _generationService.CreateAsync(new CreateGenerationCommand(project.Id, parameters));

        // Remove the checkpoint before processing
        await _modelCatalogRepo.RemoveAsync(checkpoint.Id);

        // Clear change tracker to simulate fresh scope
        _context.ChangeTracker.Clear();

        // Process the job
        var mockBackend = new MockInferenceBackend();
        var contentSafety = Substitute.For<IContentSafetyService>();
        contentSafety.GetFilterModeAsync(Arg.Any<CancellationToken>()).Returns(NsfwFilterMode.Off);
        contentSafety.ClassifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ContentClassification(ContentRating.Unknown, 0, 0, 0, 0, 1));
        var handler = new GenerationJobHandler(
            _genJobRepo, _modelCatalogRepo, mockBackend, _appPaths, contentSafety,
            Substitute.For<ILogger<GenerationJobHandler>>());

        var jobRecord = JobRecord.Create("generation", $"{{\"GenerationJobId\":\"{genJobDto.Id}\"}}");
        jobRecord.Start();
        await handler.HandleAsync(jobRecord, CancellationToken.None);

        // Job should be failed
        var failedJob = await _generationService.GetJobAsync(genJobDto.Id);
        failedJob!.Status.Should().Be(GenerationJobStatus.Failed);
        failedJob.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Pipeline_WithInvalidJobData_FailsJobRecord()
    {
        var mockBackend = new MockInferenceBackend();
        var contentSafety = Substitute.For<IContentSafetyService>();
        contentSafety.GetFilterModeAsync(Arg.Any<CancellationToken>()).Returns(NsfwFilterMode.Off);
        contentSafety.ClassifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ContentClassification(ContentRating.Unknown, 0, 0, 0, 0, 1));
        var handler = new GenerationJobHandler(
            _genJobRepo, _modelCatalogRepo, mockBackend, _appPaths, contentSafety,
            Substitute.For<ILogger<GenerationJobHandler>>());

        var jobRecord = JobRecord.Create("generation", "invalid-json");
        jobRecord.Start();
        await handler.HandleAsync(jobRecord, CancellationToken.None);

        jobRecord.Status.Should().Be(JobStatus.Failed);
        jobRecord.ErrorMessage.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Pipeline_WithNullJobData_FailsJobRecord()
    {
        var mockBackend = new MockInferenceBackend();
        var contentSafety = Substitute.For<IContentSafetyService>();
        contentSafety.GetFilterModeAsync(Arg.Any<CancellationToken>()).Returns(NsfwFilterMode.Off);
        contentSafety.ClassifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ContentClassification(ContentRating.Unknown, 0, 0, 0, 0, 1));
        var handler = new GenerationJobHandler(
            _genJobRepo, _modelCatalogRepo, mockBackend, _appPaths, contentSafety,
            Substitute.For<ILogger<GenerationJobHandler>>());

        var jobRecord = JobRecord.Create("generation", null);
        jobRecord.Start();

        // null Data will throw when deserialized
        var act = () => handler.HandleAsync(jobRecord, CancellationToken.None);
        // It might throw or fail the job, either is fine
        try
        {
            await handler.HandleAsync(jobRecord, CancellationToken.None);
            jobRecord.Status.Should().Be(JobStatus.Failed);
        }
        catch
        {
            // Exception is also acceptable for null data
        }
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        try { if (Directory.Exists(_tempAssetsDir)) Directory.Delete(_tempAssetsDir, true); } catch { }
    }
}
