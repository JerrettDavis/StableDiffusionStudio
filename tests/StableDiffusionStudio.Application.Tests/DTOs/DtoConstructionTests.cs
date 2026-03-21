using FluentAssertions;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Tests.DTOs;

/// <summary>
/// Verifies all DTOs can be constructed with sample data and have correct properties.
/// These catch breaking changes to DTO constructors.
/// </summary>
public class DtoConstructionTests
{
    [Fact]
    public void ProjectDto_CanBeConstructed()
    {
        var dto = new ProjectDto(
            Guid.NewGuid(), "Test", "Desc", ProjectStatus.Active, true,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        dto.Name.Should().Be("Test");
        dto.Description.Should().Be("Desc");
        dto.Status.Should().Be(ProjectStatus.Active);
        dto.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void GenerationJobDto_CanBeConstructed()
    {
        var parameters = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = Guid.NewGuid()
        };

        var dto = new GenerationJobDto(
            Guid.NewGuid(), Guid.NewGuid(), parameters,
            GenerationJobStatus.Completed,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            null, new List<GeneratedImageDto>());

        dto.Status.Should().Be(GenerationJobStatus.Completed);
        dto.Parameters.PositivePrompt.Should().Be("test");
        dto.Images.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedImageDto_CanBeConstructed()
    {
        var dto = new GeneratedImageDto(
            Guid.NewGuid(), Guid.NewGuid(), "/path/img.png",
            42, 512, 512, 1.5, "{}", DateTimeOffset.UtcNow, true);

        dto.FilePath.Should().Be("/path/img.png");
        dto.Seed.Should().Be(42);
        dto.Width.Should().Be(512);
        dto.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void GeneratedImageDto_DefaultIsFavorite_IsFalse()
    {
        var dto = new GeneratedImageDto(
            Guid.NewGuid(), Guid.NewGuid(), "/path/img.png",
            42, 512, 512, 1.5, "{}", DateTimeOffset.UtcNow);

        dto.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void ModelRecordDto_CanBeConstructed()
    {
        var dto = new ModelRecordDto(
            Guid.NewGuid(), "Model X", ModelType.Checkpoint, ModelFamily.SDXL,
            ModelFormat.SafeTensors, "/models/x.safetensors", 4096000,
            "local", new[] { "sdxl" }, "A model", "/preview.png",
            "Needs 12GB", ModelStatus.Available, DateTimeOffset.UtcNow);

        dto.Title.Should().Be("Model X");
        dto.Type.Should().Be(ModelType.Checkpoint);
        dto.Tags.Should().Contain("sdxl");
    }

    [Fact]
    public void GenerationPresetDto_CanBeConstructed()
    {
        var dto = new GenerationPresetDto(
            Guid.NewGuid(), "My Preset", "desc",
            Guid.NewGuid(), ModelFamily.SD15, true,
            "masterpiece", "ugly", Sampler.EulerA, Scheduler.Normal,
            20, 7.0, 512, 512, 1, 1, PresetApplyMode.Replace,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        dto.Name.Should().Be("My Preset");
        dto.IsDefault.Should().BeTrue();
        dto.ModelFamilyFilter.Should().Be(ModelFamily.SD15);
    }

    [Fact]
    public void GenerationStatusDto_CanBeConstructed()
    {
        var dto = new GenerationStatusDto(
            GenerationJobStatus.Running, 50, "Generating...", null, 2, 5.5);

        dto.Status.Should().Be(GenerationJobStatus.Running);
        dto.Progress.Should().Be(50);
        dto.Phase.Should().Be("Generating...");
        dto.ImageCount.Should().Be(2);
        dto.ElapsedSeconds.Should().Be(5.5);
    }

    [Fact]
    public void JobRecordDto_CanBeConstructed()
    {
        var dto = new JobRecordDto(
            Guid.NewGuid(), "model-scan", JobStatus.Completed, 100, "Done",
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, null, "result");

        dto.Type.Should().Be("model-scan");
        dto.Status.Should().Be(JobStatus.Completed);
        dto.Progress.Should().Be(100);
    }

    [Fact]
    public void ProjectFilter_DefaultValues()
    {
        var filter = new ProjectFilter();

        filter.SearchTerm.Should().BeNull();
        filter.Status.Should().BeNull();
        filter.IsPinned.Should().BeNull();
        filter.Skip.Should().Be(0);
        filter.Take.Should().Be(50);
    }

    [Fact]
    public void ModelFilter_DefaultValues()
    {
        var filter = new ModelFilter();

        filter.SearchTerm.Should().BeNull();
        filter.Family.Should().BeNull();
        filter.Format.Should().BeNull();
        filter.Status.Should().BeNull();
        filter.Source.Should().BeNull();
        filter.Type.Should().BeNull();
        filter.Skip.Should().Be(0);
        filter.Take.Should().Be(50);
    }

    [Fact]
    public void ScanResult_CanBeConstructed()
    {
        var dto = new ScanResult(5, 3, 1);

        dto.NewCount.Should().Be(5);
        dto.UpdatedCount.Should().Be(3);
        dto.MissingCount.Should().Be(1);
    }

    [Fact]
    public void SearchResult_CanBeConstructed()
    {
        var models = new List<RemoteModelInfo>();
        var dto = new SearchResult(models, 0, false);

        dto.Models.Should().BeEmpty();
        dto.TotalCount.Should().Be(0);
        dto.HasMore.Should().BeFalse();
    }

    [Fact]
    public void RemoteModelInfo_CanBeConstructed()
    {
        var dto = new RemoteModelInfo(
            "ext-1", "Remote Model", "A description",
            ModelType.Checkpoint, ModelFamily.SDXL, ModelFormat.SafeTensors,
            4096000, "https://img.com/preview.jpg",
            new[] { "sdxl", "photorealistic" }, "https://example.com/model",
            new List<ModelFileVariant>());

        dto.ExternalId.Should().Be("ext-1");
        dto.Title.Should().Be("Remote Model");
        dto.Tags.Should().HaveCount(2);
    }

    [Fact]
    public void ModelFileVariant_CanBeConstructed()
    {
        var dto = new ModelFileVariant("model.safetensors", 4096000, ModelFormat.SafeTensors, "fp16");

        dto.FileName.Should().Be("model.safetensors");
        dto.FileSize.Should().Be(4096000);
        dto.Format.Should().Be(ModelFormat.SafeTensors);
        dto.Quantization.Should().Be("fp16");
    }

    [Fact]
    public void ModelProviderCapabilities_CanBeConstructed()
    {
        var caps = new ModelProviderCapabilities(
            true, true, true, false, new[] { ModelType.Checkpoint, ModelType.LoRA });

        caps.CanScanLocal.Should().BeTrue();
        caps.CanSearch.Should().BeTrue();
        caps.CanDownload.Should().BeTrue();
        caps.RequiresAuth.Should().BeFalse();
        caps.SupportedModelTypes.Should().HaveCount(2);
    }

    [Fact]
    public void ModelProviderInfo_CanBeConstructed()
    {
        var caps = new ModelProviderCapabilities(true, false, false, false, Array.Empty<ModelType>());
        var dto = new ModelProviderInfo("local-folder", "Local Folder", caps);

        dto.ProviderId.Should().Be("local-folder");
        dto.DisplayName.Should().Be("Local Folder");
    }

    [Fact]
    public void DownloadRequest_CanBeConstructed()
    {
        var root = new StorageRoot("/models", "Models");
        var dto = new DownloadRequest("huggingface", "ext-1", "model.safetensors", root, ModelType.Checkpoint);

        dto.ProviderId.Should().Be("huggingface");
        dto.ExternalId.Should().Be("ext-1");
        dto.TargetRoot.Path.Should().Be("/models");
    }

    [Fact]
    public void DownloadResult_CanBeConstructed()
    {
        var success = new DownloadResult(true, "/models/file.safetensors", null);
        var failure = new DownloadResult(false, null, "Network error");

        success.Success.Should().BeTrue();
        success.LocalFilePath.Should().Be("/models/file.safetensors");
        failure.Success.Should().BeFalse();
        failure.Error.Should().Be("Network error");
    }

    [Fact]
    public void ModelSearchQuery_DefaultValues()
    {
        var query = new ModelSearchQuery("huggingface");

        query.ProviderId.Should().Be("huggingface");
        query.SearchTerm.Should().BeNull();
        query.Type.Should().BeNull();
        query.Family.Should().BeNull();
        query.Sort.Should().Be(SortOrder.Relevance);
        query.Page.Should().Be(0);
        query.PageSize.Should().Be(20);
    }

    [Fact]
    public void InferenceRequest_DefaultEta_IsZero()
    {
        var request = new InferenceRequest(
            "prompt", "neg", Sampler.Euler, Scheduler.Normal,
            20, 7.0, -1, 512, 512, 1);

        request.Eta.Should().Be(0.0);
    }

    [Fact]
    public void InferenceRequest_WithEta_SetsCorrectly()
    {
        var request = new InferenceRequest(
            "prompt", "neg", Sampler.Euler, Scheduler.Normal,
            20, 7.0, -1, 512, 512, 1, 1, 0.5);

        request.Eta.Should().Be(0.5);
    }
}
