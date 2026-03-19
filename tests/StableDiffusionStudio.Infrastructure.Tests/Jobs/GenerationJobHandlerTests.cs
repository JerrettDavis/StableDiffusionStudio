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
    private readonly ILogger<GenerationJobHandler> _logger = Substitute.For<ILogger<GenerationJobHandler>>();
    private readonly GenerationJobHandler _handler;

    private static readonly Guid CheckpointId = Guid.NewGuid();

    public GenerationJobHandlerTests()
    {
        _handler = new GenerationJobHandler(_genJobRepo, _modelCatalog, _backend, _logger);
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
        await _backend.Received(1).UnloadModelAsync(Arg.Any<CancellationToken>());
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
}
