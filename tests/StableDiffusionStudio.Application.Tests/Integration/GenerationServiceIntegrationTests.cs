using FluentAssertions;
using Microsoft.Data.Sqlite;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Application.Tests.Integration;

public class GenerationServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly GenerationService _service;
    private readonly IJobQueue _jobQueue;
    private readonly Guid _modelId;
    private readonly Guid _projectId;

    public GenerationServiceIntegrationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var genRepo = new GenerationJobRepository(_context);
        var modelRepo = new ModelCatalogRepository(_context);
        _jobQueue = Substitute.For<IJobQueue>();
        _jobQueue.EnqueueAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid());

        var appPaths = Substitute.For<IAppPaths>();
        appPaths.GetProjectAssetsDirectory(Arg.Any<Guid>()).Returns("/tmp/assets");
        _service = new GenerationService(genRepo, modelRepo, _jobQueue, appPaths);

        // Seed a model and a project
        var model = ModelRecord.Create("Test Checkpoint", "/models/test.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2048000, "local");
        _context.ModelRecords.Add(model);
        _modelId = model.Id;

        var project = Project.Create("Test Project", null);
        _context.Projects.Add(project);
        _projectId = project.Id;

        _context.SaveChanges();
    }

    private GenerationParameters CreateValidParams() => new()
    {
        PositivePrompt = "a beautiful sunset",
        CheckpointModelId = _modelId,
        Steps = 20,
        CfgScale = 7.0,
        Width = 512,
        Height = 512,
        BatchSize = 1
    };

    [Fact]
    public async Task CreateAsync_PersistsJobAndEnqueuesWork()
    {
        var command = new CreateGenerationCommand(_projectId, CreateValidParams());

        var dto = await _service.CreateAsync(command);

        dto.Id.Should().NotBeEmpty();
        dto.ProjectId.Should().Be(_projectId);
        dto.Status.Should().Be(GenerationJobStatus.Pending);
        dto.Parameters.PositivePrompt.Should().Be("a beautiful sunset");

        await _jobQueue.Received(1).EnqueueAsync("generation", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithMissingCheckpoint_Throws()
    {
        var parameters = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = Guid.NewGuid(), // doesn't exist
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var command = new CreateGenerationCommand(_projectId, parameters);

        var act = () => _service.CreateAsync(command);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetJobAsync_ReturnsJobWithImages()
    {
        var command = new CreateGenerationCommand(_projectId, CreateValidParams());
        var created = await _service.CreateAsync(command);

        // Add an image directly to DB (simulating what the background job handler does)
        var image = GeneratedImage.Create(created.Id, "/output/img1.png", 42, 512, 512, 1.5, "{}");
        _context.GeneratedImages.Add(image);
        await _context.SaveChangesAsync();

        var dto = await _service.GetJobAsync(created.Id);

        dto.Should().NotBeNull();
        dto!.Images.Should().HaveCount(1);
        dto.Images[0].Seed.Should().Be(42);
        dto.Images[0].FilePath.Should().Be("/output/img1.png");
    }

    [Fact]
    public async Task GetJobStatusAsync_ReturnsCorrectStatus()
    {
        var command = new CreateGenerationCommand(_projectId, CreateValidParams());
        var created = await _service.CreateAsync(command);

        var status = await _service.GetJobStatusAsync(created.Id);

        status.Should().NotBeNull();
        status!.Status.Should().Be(GenerationJobStatus.Pending);
        status.ImageCount.Should().Be(0);
    }

    [Fact]
    public async Task GetJobStatusAsync_NonExistent_ReturnsNull()
    {
        var status = await _service.GetJobStatusAsync(Guid.NewGuid());

        status.Should().BeNull();
    }

    [Fact]
    public async Task CancelGenerationAsync_MarksAsCancelled()
    {
        var command = new CreateGenerationCommand(_projectId, CreateValidParams());
        var created = await _service.CreateAsync(command);

        // Clear the change tracker so GetByIdAsync (AsNoTracking) + Attach works cleanly
        _context.ChangeTracker.Clear();

        await _service.CancelGenerationAsync(created.Id);

        var dto = await _service.GetJobAsync(created.Id);
        dto!.Status.Should().Be(GenerationJobStatus.Cancelled);
    }

    [Fact]
    public async Task CancelGenerationAsync_NonExistent_Throws()
    {
        var act = () => _service.CancelGenerationAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ListJobsForProjectAsync_OrdersByNewest()
    {
        var p1 = CreateValidParams();
        var p2 = new GenerationParameters
        {
            PositivePrompt = "second job",
            CheckpointModelId = _modelId,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };

        await _service.CreateAsync(new CreateGenerationCommand(_projectId, p1));
        await Task.Delay(10); // ensure different CreatedAt
        await _service.CreateAsync(new CreateGenerationCommand(_projectId, p2));

        var list = await _service.ListJobsForProjectAsync(_projectId);

        list.Should().HaveCount(2);
        list[0].Parameters.PositivePrompt.Should().Be("second job"); // newest first
    }

    [Fact]
    public async Task ToggleFavoriteAsync_TogglesFlag()
    {
        var command = new CreateGenerationCommand(_projectId, CreateValidParams());
        var created = await _service.CreateAsync(command);

        var image = GeneratedImage.Create(created.Id, "/output/fav.png", 99, 512, 512, 1.0, "{}");
        _context.GeneratedImages.Add(image);
        await _context.SaveChangesAsync();

        // ToggleFavorite uses tracked query (FirstOrDefaultAsync without AsNoTracking), so it works
        await _service.ToggleFavoriteAsync(image.Id);

        // Clear tracker before re-reading with AsNoTracking
        _context.ChangeTracker.Clear();

        var dto = await _service.GetJobAsync(created.Id);
        dto!.Images.First(i => i.Id == image.Id).IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleFavoriteAsync_NonExistent_Throws()
    {
        var act = () => _service.ToggleFavoriteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CloneParametersAsync_ReturnsMatchingParams()
    {
        var originalParams = CreateValidParams();
        var command = new CreateGenerationCommand(_projectId, originalParams);
        var created = await _service.CreateAsync(command);

        var cloned = await _service.CloneParametersAsync(created.Id);

        cloned.PositivePrompt.Should().Be(originalParams.PositivePrompt);
        cloned.CheckpointModelId.Should().Be(originalParams.CheckpointModelId);
        cloned.Steps.Should().Be(originalParams.Steps);
        cloned.CfgScale.Should().Be(originalParams.CfgScale);
        cloned.Width.Should().Be(originalParams.Width);
        cloned.Height.Should().Be(originalParams.Height);
    }

    [Fact]
    public async Task CloneParametersAsync_NonExistent_Throws()
    {
        var act = () => _service.CloneParametersAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
