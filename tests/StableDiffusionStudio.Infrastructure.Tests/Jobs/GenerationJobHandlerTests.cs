using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Jobs;

namespace StableDiffusionStudio.Infrastructure.Tests.Jobs;

public class GenerationJobHandlerTests
{
    private readonly IGenerationJobRepository _genJobRepo = Substitute.For<IGenerationJobRepository>();
    private readonly IModelCatalogRepository _modelCatalog = Substitute.For<IModelCatalogRepository>();
    private readonly IInferenceBackend _backend = Substitute.For<IInferenceBackend>();
    private readonly IAppPaths _appPaths = Substitute.For<IAppPaths>();
    private readonly IContentSafetyService _contentSafety = Substitute.For<IContentSafetyService>();
    private readonly ILogger<GenerationJobHandler> _logger = Substitute.For<ILogger<GenerationJobHandler>>();
    private readonly GenerationJobHandler _handler;

    private static readonly Guid CheckpointId = Guid.NewGuid();

    public GenerationJobHandlerTests()
    {
        _appPaths.GetJobAssetsDirectory(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(ci => Path.Combine(Path.GetTempPath(), "SDS_Test_Assets",
                ci.ArgAt<Guid>(0).ToString(), ci.ArgAt<Guid>(1).ToString()));
        _contentSafety.GetFilterModeAsync(Arg.Any<CancellationToken>())
            .Returns(Domain.Enums.NsfwFilterMode.Off);
        _contentSafety.ClassifyAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new ContentClassification(Domain.Enums.ContentRating.Unknown, 0, 0, 0, 0, 1));
        _handler = new GenerationJobHandler(_genJobRepo, _modelCatalog, _backend, _appPaths, _contentSafety, _logger);
    }

    private static GenerationParameters ValidParameters => new()
    {
        PositivePrompt = "a beautiful landscape",
        CheckpointModelId = CheckpointId,
        Steps = 5,
        BatchSize = 1,
        Seed = 42
    };

    private void SetupCheckpoint()
    {
        var model = ModelRecord.Create("Test Checkpoint", "/models/test.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 1024, "local");
        // Use reflection or direct substitute to return the model with the correct Id
        _modelCatalog.GetByIdAsync(CheckpointId, Arg.Any<CancellationToken>()).Returns(model);
    }

    private void SetupSuccessfulGeneration()
    {
        _backend.GenerateAsync(Arg.Any<InferenceRequest>(), Arg.Any<IProgress<InferenceProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new InferenceResult(true,
                [new GeneratedImageData([0x89, 0x50, 0x4E, 0x47], 42, 1.5)], null));
    }

    [Fact]
    public async Task HandleAsync_InvalidData_FailsJob()
    {
        var jobRecord = JobRecord.Create("generation", "not-valid-json{{{");
        jobRecord.Start();

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        jobRecord.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task HandleAsync_GenerationJobNotFound_FailsJob()
    {
        var genJob = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = Guid.NewGuid() });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((GenerationJob?)null);

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        jobRecord.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task HandleAsync_CheckpointNotFound_FailsGenerationJob()
    {
        var genJob = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        _modelCatalog.GetByIdAsync(CheckpointId, Arg.Any<CancellationToken>()).Returns((ModelRecord?)null);

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        genJob.Status.Should().Be(GenerationJobStatus.Failed);
        jobRecord.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task HandleAsync_SuccessfulGeneration_CompletesJob()
    {
        var genJob = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        SetupSuccessfulGeneration();

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        genJob.Status.Should().Be(GenerationJobStatus.Completed);
        genJob.Images.Should().HaveCount(1);
        await _backend.Received(1).LoadModelAsync(Arg.Any<ModelLoadRequest>(), Arg.Any<CancellationToken>());
        await _backend.Received(1).GenerateAsync(Arg.Any<InferenceRequest>(), Arg.Any<IProgress<InferenceProgress>>(), Arg.Any<CancellationToken>());
        // Model is intentionally kept loaded for reuse
        await _backend.DidNotReceive().UnloadModelAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SuccessfulGeneration_SavesImagesToRepo()
    {
        var genJob = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        SetupSuccessfulGeneration();

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        await _genJobRepo.Received().UpdateAsync(genJob, Arg.Any<CancellationToken>());
        genJob.Images.Should().HaveCount(1);
        genJob.Images[0].Seed.Should().Be(42);
    }

    [Fact]
    public async Task HandleAsync_FailedGeneration_FailsGenerationJob()
    {
        var genJob = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        _backend.GenerateAsync(Arg.Any<InferenceRequest>(), Arg.Any<IProgress<InferenceProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new InferenceResult(false, [], "GPU out of memory"));

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        genJob.Status.Should().Be(GenerationJobStatus.Failed);
        genJob.ErrorMessage.Should().Be("GPU out of memory");
    }

    [Fact]
    public async Task HandleAsync_MapsParametersToInferenceRequest()
    {
        var parameters = ValidParameters with
        {
            NegativePrompt = "ugly",
            Sampler = Sampler.DPMPlusPlus2MKarras,
            Scheduler = Scheduler.Karras,
            CfgScale = 9.0,
            Width = 768,
            Height = 512
        };
        var genJob = GenerationJob.Create(Guid.NewGuid(), parameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        SetupSuccessfulGeneration();

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        await _backend.Received(1).GenerateAsync(
            Arg.Is<InferenceRequest>(r =>
                r.PositivePrompt == "a beautiful landscape" &&
                r.NegativePrompt == "ugly" &&
                r.Sampler == Sampler.DPMPlusPlus2MKarras &&
                r.Scheduler == Scheduler.Karras &&
                r.CfgScale == 9.0 &&
                r.Width == 768 &&
                r.Height == 512),
            Arg.Any<IProgress<InferenceProgress>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithBatchCountGreaterThanOne_RunsMultipleBatches()
    {
        var parameters = ValidParameters with { BatchCount = 3 };
        var genJob = GenerationJob.Create(Guid.NewGuid(), parameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        SetupSuccessfulGeneration();

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        genJob.Status.Should().Be(GenerationJobStatus.Completed);
        await _backend.Received(3).GenerateAsync(
            Arg.Any<InferenceRequest>(),
            Arg.Any<IProgress<InferenceProgress>>(),
            Arg.Any<CancellationToken>());
        genJob.Images.Should().HaveCount(3);
    }

    [Fact]
    public async Task HandleAsync_WithVaeModelId_ResolvesVaePath()
    {
        var vaeId = Guid.NewGuid();
        var parameters = ValidParameters with { VaeModelId = vaeId };
        var genJob = GenerationJob.Create(Guid.NewGuid(), parameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        SetupSuccessfulGeneration();

        var vaeModel = ModelRecord.Create("Test VAE", "/models/vae.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 500, "local", ModelType.VAE);
        _modelCatalog.GetByIdAsync(vaeId, Arg.Any<CancellationToken>()).Returns(vaeModel);

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        await _backend.Received(1).LoadModelAsync(
            Arg.Is<ModelLoadRequest>(r => r.VaePath == "/models/vae.safetensors"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithLoRAs_ResolvesLoraPaths()
    {
        var loraId = Guid.NewGuid();
        var parameters = ValidParameters with { Loras = [new LoraReference(loraId, 0.8)] };
        var genJob = GenerationJob.Create(Guid.NewGuid(), parameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        SetupSuccessfulGeneration();

        var loraModel = ModelRecord.Create("Test LoRA", "/models/lora.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 100, "local", ModelType.LoRA);
        _modelCatalog.GetByIdAsync(loraId, Arg.Any<CancellationToken>()).Returns(loraModel);

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        await _backend.Received(1).LoadModelAsync(
            Arg.Is<ModelLoadRequest>(r =>
                r.Loras.Count == 1 &&
                r.Loras[0].Path == "/models/lora.safetensors" &&
                r.Loras[0].Weight == 0.8),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_BackendThrowsException_FailsJob()
    {
        var genJob = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        _backend.GenerateAsync(Arg.Any<InferenceRequest>(), Arg.Any<IProgress<InferenceProgress>>(), Arg.Any<CancellationToken>())
            .Returns<InferenceResult>(_ => throw new InvalidOperationException("Native crash"));

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        genJob.Status.Should().Be(GenerationJobStatus.Failed);
        genJob.ErrorMessage.Should().Contain("Native crash");
        jobRecord.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task HandleAsync_EmptyGuidInData_FailsJob()
    {
        var data = JsonSerializer.Serialize(new { GenerationJobId = Guid.Empty });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        jobRecord.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task HandleAsync_WithDefaultSeed_PassesSeedToBackend()
    {
        var parameters = ValidParameters with { Seed = -1 };
        var genJob = GenerationJob.Create(Guid.NewGuid(), parameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();
        SetupSuccessfulGeneration();

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        await _backend.Received(1).GenerateAsync(
            Arg.Is<InferenceRequest>(r => r.Seed == -1),
            Arg.Any<IProgress<InferenceProgress>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SecondBatchFails_FailsEntireJob()
    {
        var parameters = ValidParameters with { BatchCount = 2 };
        var genJob = GenerationJob.Create(Guid.NewGuid(), parameters);
        var data = JsonSerializer.Serialize(new { GenerationJobId = genJob.Id });
        var jobRecord = JobRecord.Create("generation", data);
        jobRecord.Start();

        _genJobRepo.GetByIdAsync(genJob.Id, Arg.Any<CancellationToken>()).Returns(genJob);
        SetupCheckpoint();

        var callCount = 0;
        _backend.GenerateAsync(Arg.Any<InferenceRequest>(), Arg.Any<IProgress<InferenceProgress>>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new InferenceResult(true, [new GeneratedImageData([0x89, 0x50, 0x4E, 0x47], 42, 1.0)], null);
                return new InferenceResult(false, [], "Out of VRAM on batch 2");
            });

        await _handler.HandleAsync(jobRecord, CancellationToken.None);

        genJob.Status.Should().Be(GenerationJobStatus.Failed);
        genJob.ErrorMessage.Should().Contain("Out of VRAM on batch 2");
    }
}
