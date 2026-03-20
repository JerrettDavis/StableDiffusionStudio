using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Services;
using StableDiffusionStudio.Infrastructure.Tests.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Services;

public class DataManagementServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly DataManagementService _service;

    public DataManagementServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var appPaths = Substitute.For<IAppPaths>();
        appPaths.AssetsDirectory.Returns(Path.Combine(Path.GetTempPath(), "SDS_Test_Assets"));
        _service = new DataManagementService(_context, appPaths);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetUsageSummaryAsync_ReturnsCorrectCounts()
    {
        // Arrange
        var project = Project.Create("Test", "Desc");
        _context.Projects.Add(project);

        var model = ModelRecord.Create("Model1", "/fake/path.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var jobRecord = JobRecord.Create("model-scan");
        _context.JobRecords.Add(jobRecord);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test prompt",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob = GenerationJob.Create(project.Id, genParams);
        _context.GenerationJobs.Add(genJob);

        var image = GeneratedImage.Create(genJob.Id, "/fake/image.png", 42, 512, 512, 1.5, "{}");
        _context.GeneratedImages.Add(image);

        await _context.SaveChangesAsync();

        // Act
        var summary = await _service.GetUsageSummaryAsync();

        // Assert
        summary.ProjectCount.Should().Be(1);
        summary.ModelRecordCount.Should().Be(1);
        summary.GenerationJobCount.Should().Be(1);
        summary.GeneratedImageCount.Should().Be(1);
        summary.JobRecordCount.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAllGeneratedImagesAsync_RemovesAllImages()
    {
        // Arrange
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var model = ModelRecord.Create("M", "/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob = GenerationJob.Create(project.Id, genParams);
        _context.GenerationJobs.Add(genJob);

        _context.GeneratedImages.Add(GeneratedImage.Create(genJob.Id, "/fake/1.png", 1, 512, 512, 1.0, "{}"));
        _context.GeneratedImages.Add(GeneratedImage.Create(genJob.Id, "/fake/2.png", 2, 512, 512, 1.0, "{}"));
        await _context.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteAllGeneratedImagesAsync();

        // Assert
        deleted.Should().Be(2);
        (await _context.GeneratedImages.CountAsync()).Should().Be(0);
        // Generation jobs should still exist
        (await _context.GenerationJobs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteCompletedJobRecordsAsync_OnlyRemovesCompleted()
    {
        // Arrange
        var completed = JobRecord.Create("scan");
        completed.Start();
        completed.Complete();
        _context.JobRecords.Add(completed);

        var failed = JobRecord.Create("scan");
        failed.Start();
        failed.Fail("error");
        _context.JobRecords.Add(failed);

        var pending = JobRecord.Create("scan");
        _context.JobRecords.Add(pending);

        await _context.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteCompletedJobRecordsAsync();

        // Assert
        deleted.Should().Be(1);
        var remaining = await _context.JobRecords.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(j => j.Status == JobStatus.Completed);
    }

    [Fact]
    public async Task DeleteAllModelRecordsAsync_ClearsModelCatalog()
    {
        // Arrange
        _context.ModelRecords.Add(ModelRecord.Create("M1", "/p1.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local"));
        _context.ModelRecords.Add(ModelRecord.Create("M2", "/p2.ckpt",
            ModelFamily.SDXL, ModelFormat.CKPT, 2048, "hf"));
        await _context.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteAllModelRecordsAsync();

        // Assert
        deleted.Should().Be(2);
        (await _context.ModelRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ResetAllDataAsync_ClearsEverythingExceptSettings()
    {
        // Arrange
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var model = ModelRecord.Create("M", "/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var jobRecord = JobRecord.Create("scan");
        _context.JobRecords.Add(jobRecord);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob = GenerationJob.Create(project.Id, genParams);
        _context.GenerationJobs.Add(genJob);

        _context.GeneratedImages.Add(GeneratedImage.Create(genJob.Id, "/fake/1.png", 1, 512, 512, 1.0, "{}"));

        var setting = Setting.Create("theme", "dark");
        _context.Settings.Add(setting);

        await _context.SaveChangesAsync();

        // Act
        await _service.ResetAllDataAsync();

        // Assert
        (await _context.Projects.CountAsync()).Should().Be(0);
        (await _context.ModelRecords.CountAsync()).Should().Be(0);
        (await _context.JobRecords.CountAsync()).Should().Be(0);
        (await _context.GenerationJobs.CountAsync()).Should().Be(0);
        (await _context.GeneratedImages.CountAsync()).Should().Be(0);
        // Settings must be preserved
        (await _context.Settings.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteGenerationJobAsync_CascadesToImages()
    {
        // Arrange
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var model = ModelRecord.Create("M", "/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob = GenerationJob.Create(project.Id, genParams);
        _context.GenerationJobs.Add(genJob);
        _context.GeneratedImages.Add(GeneratedImage.Create(genJob.Id, "/fake/1.png", 1, 512, 512, 1.0, "{}"));
        _context.GeneratedImages.Add(GeneratedImage.Create(genJob.Id, "/fake/2.png", 2, 512, 512, 1.0, "{}"));
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteGenerationJobAsync(genJob.Id);

        // Assert
        (await _context.GenerationJobs.CountAsync()).Should().Be(0);
        (await _context.GeneratedImages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteGeneratedImageAsync_RemovesSingleImage()
    {
        // Arrange
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var model = ModelRecord.Create("M", "/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob = GenerationJob.Create(project.Id, genParams);
        _context.GenerationJobs.Add(genJob);
        var image1 = GeneratedImage.Create(genJob.Id, "/fake/1.png", 1, 512, 512, 1.0, "{}");
        var image2 = GeneratedImage.Create(genJob.Id, "/fake/2.png", 2, 512, 512, 1.0, "{}");
        _context.GeneratedImages.Add(image1);
        _context.GeneratedImages.Add(image2);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteGeneratedImageAsync(image1.Id);

        // Assert
        (await _context.GeneratedImages.CountAsync()).Should().Be(1);
        (await _context.GeneratedImages.FindAsync(image2.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CleanOrphanedAssetsAsync_WithNoOrphans_ReturnsZero()
    {
        // The assets directory doesn't exist or has no orphaned files
        var result = await _service.CleanOrphanedAssetsAsync();
        result.Should().Be(0);
    }

    [Fact]
    public async Task DeleteFailedJobRecordsAsync_OnlyRemovesFailed()
    {
        // Arrange
        var completed = JobRecord.Create("scan");
        completed.Start();
        completed.Complete();
        _context.JobRecords.Add(completed);

        var failed = JobRecord.Create("scan");
        failed.Start();
        failed.Fail("error");
        _context.JobRecords.Add(failed);

        var pending = JobRecord.Create("scan");
        _context.JobRecords.Add(pending);

        await _context.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteFailedJobRecordsAsync();

        // Assert
        deleted.Should().Be(1);
        var remaining = await _context.JobRecords.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(j => j.Status == JobStatus.Failed);
    }

    [Fact]
    public async Task DeleteAllProjectsAsync_CascadesToEverything()
    {
        // Arrange
        var project1 = Project.Create("Project 1", null);
        var project2 = Project.Create("Project 2", null);
        _context.Projects.Add(project1);
        _context.Projects.Add(project2);

        var model = ModelRecord.Create("M", "/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob = GenerationJob.Create(project1.Id, genParams);
        _context.GenerationJobs.Add(genJob);
        _context.GeneratedImages.Add(GeneratedImage.Create(genJob.Id, "/fake/1.png", 1, 512, 512, 1.0, "{}"));
        await _context.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteAllProjectsAsync();

        // Assert
        deleted.Should().Be(2);
        (await _context.Projects.CountAsync()).Should().Be(0);
        (await _context.GenerationJobs.CountAsync()).Should().Be(0);
        (await _context.GeneratedImages.CountAsync()).Should().Be(0);
        // Models should still exist
        (await _context.ModelRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteAllJobRecordsAsync_ClearsAllRecords()
    {
        _context.JobRecords.Add(JobRecord.Create("scan"));
        _context.JobRecords.Add(JobRecord.Create("download"));
        await _context.SaveChangesAsync();

        var deleted = await _service.DeleteAllJobRecordsAsync();

        deleted.Should().Be(2);
        (await _context.JobRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteGenerationJobsAsync_RemovesAllJobsAndImages()
    {
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var model = ModelRecord.Create("M", "/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob1 = GenerationJob.Create(project.Id, genParams);
        var genJob2 = GenerationJob.Create(project.Id, genParams);
        _context.GenerationJobs.Add(genJob1);
        _context.GenerationJobs.Add(genJob2);
        _context.GeneratedImages.Add(GeneratedImage.Create(genJob1.Id, "/fake/1.png", 1, 512, 512, 1.0, "{}"));
        _context.GeneratedImages.Add(GeneratedImage.Create(genJob2.Id, "/fake/2.png", 2, 512, 512, 1.0, "{}"));
        await _context.SaveChangesAsync();

        var deleted = await _service.DeleteGenerationJobsAsync();

        deleted.Should().Be(2);
        (await _context.GenerationJobs.CountAsync()).Should().Be(0);
        (await _context.GeneratedImages.CountAsync()).Should().Be(0);
        // Project should still exist
        (await _context.Projects.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetUsageSummaryAsync_EmptyDb_ReturnsZeros()
    {
        var summary = await _service.GetUsageSummaryAsync();

        summary.ProjectCount.Should().Be(0);
        summary.ModelRecordCount.Should().Be(0);
        summary.GenerationJobCount.Should().Be(0);
        summary.GeneratedImageCount.Should().Be(0);
        summary.JobRecordCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAssetsDiskUsageAsync_NonExistentDir_ReturnsZero()
    {
        var usage = await _service.GetAssetsDiskUsageAsync();
        usage.Should().Be(0);
    }

    [Fact]
    public async Task DeleteProjectAsync_NonExistent_ReturnsZero()
    {
        var deleted = await _service.DeleteProjectAsync(Guid.NewGuid());
        deleted.Should().Be(0);
    }

    [Fact]
    public async Task DeleteGeneratedImageAsync_NonExistent_DoesNotThrow()
    {
        await _service.DeleteGeneratedImageAsync(Guid.NewGuid());
        // Should not throw
    }

    [Fact]
    public async Task DeleteGenerationJobAsync_NonExistent_DoesNotThrow()
    {
        await _service.DeleteGenerationJobAsync(Guid.NewGuid());
        // Should not throw
    }

    [Fact]
    public async Task DeleteProjectAsync_CascadesToGenerationJobs()
    {
        // Arrange
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var model = ModelRecord.Create("M", "/m.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        _context.ModelRecords.Add(model);

        var genParams = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = model.Id,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        };
        var genJob = GenerationJob.Create(project.Id, genParams);
        _context.GenerationJobs.Add(genJob);
        _context.GeneratedImages.Add(GeneratedImage.Create(genJob.Id, "/fake/1.png", 1, 512, 512, 1.0, "{}"));
        await _context.SaveChangesAsync();

        // Act
        var deleted = await _service.DeleteProjectAsync(project.Id);

        // Assert
        deleted.Should().Be(1);
        (await _context.Projects.CountAsync()).Should().Be(0);
        (await _context.GenerationJobs.CountAsync()).Should().Be(0);
        (await _context.GeneratedImages.CountAsync()).Should().Be(0);
    }
}
