using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Persistence;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests that EF Core configurations (column types, conversions, indexes, etc.)
/// work correctly with real SQLite, covering the Configurations/*.cs files.
/// </summary>
public class EfConfigurationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;

    public EfConfigurationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
    }

    [Fact]
    public async Task GenerationJob_Parameters_RoundTripsAsJson()
    {
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var loraId = Guid.NewGuid();
        var parameters = new GenerationParameters
        {
            PositivePrompt = "a cat, masterpiece",
            NegativePrompt = "ugly",
            CheckpointModelId = Guid.NewGuid(),
            VaeModelId = Guid.NewGuid(),
            Loras = [new LoraReference(loraId, 0.8)],
            Sampler = Sampler.DPMPlusPlus2MKarras,
            Scheduler = Scheduler.Karras,
            Steps = 30,
            CfgScale = 7.5,
            Seed = 42,
            Width = 1024,
            Height = 1024,
            BatchSize = 4,
            ClipSkip = 2,
            BatchCount = 2
        };
        var job = GenerationJob.Create(project.Id, parameters);
        _context.GenerationJobs.Add(job);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.GenerationJobs
            .Include(j => j.Images)
            .FirstAsync(j => j.Id == job.Id);

        loaded.Parameters.PositivePrompt.Should().Be("a cat, masterpiece");
        loaded.Parameters.NegativePrompt.Should().Be("ugly");
        loaded.Parameters.Sampler.Should().Be(Sampler.DPMPlusPlus2MKarras);
        loaded.Parameters.Scheduler.Should().Be(Scheduler.Karras);
        loaded.Parameters.Steps.Should().Be(30);
        loaded.Parameters.CfgScale.Should().Be(7.5);
        loaded.Parameters.Seed.Should().Be(42);
        loaded.Parameters.Width.Should().Be(1024);
        loaded.Parameters.Height.Should().Be(1024);
        loaded.Parameters.BatchSize.Should().Be(4);
        loaded.Parameters.ClipSkip.Should().Be(2);
        loaded.Parameters.Loras.Should().HaveCount(1);
        loaded.Parameters.Loras[0].ModelId.Should().Be(loraId);
        loaded.Parameters.Loras[0].Weight.Should().Be(0.8);
    }

    [Fact]
    public async Task ModelRecord_Tags_RoundTripsAsJson()
    {
        var model = ModelRecord.Create("Tagged Model", "/models/tagged.safetensors",
            ModelFamily.SDXL, ModelFormat.SafeTensors, 4096, "local");
        model.UpdateMetadata(tags: new[] { "portrait", "realistic", "sdxl" });
        _context.ModelRecords.Add(model);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.ModelRecords.FindAsync(model.Id);
        loaded!.Tags.Should().HaveCount(3);
        loaded.Tags.Should().Contain("portrait");
        loaded.Tags.Should().Contain("realistic");
        loaded.Tags.Should().Contain("sdxl");
    }

    [Fact]
    public async Task ModelRecord_AllFields_PersistCorrectly()
    {
        var model = ModelRecord.Create("Full Model", "/models/full.ckpt",
            ModelFamily.Flux, ModelFormat.CKPT, 8192000, "civitai", ModelType.Checkpoint);
        model.UpdateMetadata(
            description: "A flux model",
            previewImagePath: "/previews/full.png",
            compatibilityHints: "Requires 24GB VRAM");
        _context.ModelRecords.Add(model);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.ModelRecords.FindAsync(model.Id);
        loaded!.Title.Should().Be("Full Model");
        loaded.Type.Should().Be(ModelType.Checkpoint);
        loaded.ModelFamily.Should().Be(ModelFamily.Flux);
        loaded.Format.Should().Be(ModelFormat.CKPT);
        loaded.FilePath.Should().Be("/models/full.ckpt");
        loaded.FileSize.Should().Be(8192000);
        loaded.Source.Should().Be("civitai");
        loaded.Description.Should().Be("A flux model");
        loaded.PreviewImagePath.Should().Be("/previews/full.png");
        loaded.CompatibilityHints.Should().Be("Requires 24GB VRAM");
        loaded.Status.Should().Be(ModelStatus.Available);
    }

    [Fact]
    public async Task Project_AllFields_PersistCorrectly()
    {
        var project = Project.Create("My Project", "A description");
        project.Pin();
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.Projects.FindAsync(project.Id);
        loaded!.Name.Should().Be("My Project");
        loaded.Description.Should().Be("A description");
        loaded.Status.Should().Be(ProjectStatus.Active);
        loaded.IsPinned.Should().BeTrue();
        loaded.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GeneratedImage_AllFields_PersistCorrectly()
    {
        var project = Project.Create("Test", null);
        _context.Projects.Add(project);

        var modelId = Guid.NewGuid();
        var job = GenerationJob.Create(project.Id, new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = modelId,
            Steps = 20,
            CfgScale = 7.0,
            Width = 512,
            Height = 512,
            BatchSize = 1
        });
        _context.GenerationJobs.Add(job);

        var image = GeneratedImage.Create(job.Id, "/output/test.png", 12345, 768, 768, 2.5, "{\"key\":\"value\"}");
        image.ToggleFavorite();
        _context.GeneratedImages.Add(image);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.GeneratedImages.FindAsync(image.Id);
        loaded!.GenerationJobId.Should().Be(job.Id);
        loaded.FilePath.Should().Be("/output/test.png");
        loaded.Seed.Should().Be(12345);
        loaded.Width.Should().Be(768);
        loaded.Height.Should().Be(768);
        loaded.GenerationTimeSeconds.Should().Be(2.5);
        loaded.ParametersJson.Should().Contain("key");
        loaded.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task JobRecord_AllFields_PersistCorrectly()
    {
        var correlationId = Guid.NewGuid();
        var job = JobRecord.Create("model-scan", "scan-data", correlationId);
        job.Start();
        job.UpdateProgress(50, "Scanning models...");
        job.Complete("result-data");
        _context.JobRecords.Add(job);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.JobRecords.FindAsync(job.Id);
        loaded!.Type.Should().Be("model-scan");
        loaded.Status.Should().Be(JobStatus.Completed);
        loaded.Progress.Should().Be(100); // Complete sets to 100
        loaded.Phase.Should().Be("Scanning models...");
        loaded.CorrelationId.Should().Be(correlationId);
        loaded.ResultData.Should().Be("result-data");
        loaded.StartedAt.Should().NotBeNull();
        loaded.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerationPreset_AllFields_PersistCorrectly()
    {
        var modelId = Guid.NewGuid();
        var preset = GenerationPresetEntity.Create(
            "My Preset", "desc", modelId, ModelFamily.SDXL,
            "masterpiece, {prompt}", "ugly, blurry",
            Sampler.DPMPlusPlus2M, Scheduler.Exponential,
            30, 8.5, 1024, 1024, 2, 2);
        preset.SetDefault(true);
        _context.GenerationPresets.Add(preset);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.GenerationPresets.FindAsync(preset.Id);
        loaded!.Name.Should().Be("My Preset");
        loaded.Description.Should().Be("desc");
        loaded.AssociatedModelId.Should().Be(modelId);
        loaded.ModelFamilyFilter.Should().Be(ModelFamily.SDXL);
        loaded.IsDefault.Should().BeTrue();
        loaded.PositivePromptTemplate.Should().Be("masterpiece, {prompt}");
        loaded.NegativePrompt.Should().Be("ugly, blurry");
        loaded.Sampler.Should().Be(Sampler.DPMPlusPlus2M);
        loaded.Scheduler.Should().Be(Scheduler.Exponential);
        loaded.Steps.Should().Be(30);
        loaded.CfgScale.Should().Be(8.5);
        loaded.Width.Should().Be(1024);
        loaded.Height.Should().Be(1024);
        loaded.BatchSize.Should().Be(2);
        loaded.ClipSkip.Should().Be(2);
    }

    [Fact]
    public async Task PromptHistory_AllFields_PersistCorrectly()
    {
        var entry = PromptHistory.Create("a beautiful sunset", "ugly, deformed");
        entry.IncrementUsage();
        entry.IncrementUsage();
        _context.PromptHistories.Add(entry);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.PromptHistories.FindAsync(entry.Id);
        loaded!.PositivePrompt.Should().Be("a beautiful sunset");
        loaded.NegativePrompt.Should().Be("ugly, deformed");
        loaded.UseCount.Should().Be(3);
    }

    [Fact]
    public async Task Setting_AllFields_PersistCorrectly()
    {
        var setting = Setting.Create("theme", "dark");
        _context.Settings.Add(setting);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        var loaded = await _context.Settings.FindAsync("theme");
        loaded!.Value.Should().Be("dark");
        loaded.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
