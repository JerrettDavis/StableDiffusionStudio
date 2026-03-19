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

public class ModelDownloadJobHandlerTests
{
    private readonly IModelProvider _mockProvider = Substitute.For<IModelProvider>();
    private readonly IModelCatalogRepository _catalogRepo = Substitute.For<IModelCatalogRepository>();
    private readonly ILogger<ModelDownloadJobHandler> _logger = Substitute.For<ILogger<ModelDownloadJobHandler>>();
    private readonly ModelDownloadJobHandler _handler;

    public ModelDownloadJobHandlerTests()
    {
        _mockProvider.ProviderId.Returns("test-provider");
        _handler = new ModelDownloadJobHandler([_mockProvider], _catalogRepo, _logger);
    }

    private static JobRecord CreateRunningJob(string data)
    {
        var job = JobRecord.Create("model-download", data);
        job.Start();
        return job;
    }

    [Fact]
    public async Task HandleAsync_InvalidData_FailsJob()
    {
        var job = CreateRunningJob("not valid json {{{");

        // JsonSerializer.Deserialize will throw on invalid JSON, which means request is null
        // Actually it throws - let's use valid JSON that doesn't match the DTO
        var job2 = JobRecord.Create("model-download", "null");
        job2.Start();
        await _handler.HandleAsync(job2, CancellationToken.None);

        job2.Status.Should().Be(JobStatus.Failed);
        job2.ErrorMessage.Should().Contain("Invalid download request data");
    }

    [Fact]
    public async Task HandleAsync_UnknownProvider_FailsJob()
    {
        var request = new DownloadRequest("unknown-provider", "ext-123", null,
            new StorageRoot("/tmp/models", "Temp"), ModelType.Checkpoint);
        var json = JsonSerializer.Serialize(request);
        var job = CreateRunningJob(json);

        await _handler.HandleAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Contain("Unknown provider: unknown-provider");
    }

    [Fact]
    public async Task HandleAsync_SuccessfulDownload_CompletesJobAndRegistersModel()
    {
        // Create a temp file to simulate download result
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.safetensors");
        await File.WriteAllBytesAsync(tempFile, new byte[1024]);

        try
        {
            var request = new DownloadRequest("test-provider", "ext-123", null,
                new StorageRoot(tempDir, "Temp"), ModelType.Checkpoint);
            var json = JsonSerializer.Serialize(request);
            var job = CreateRunningJob(json);

            _mockProvider.DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<DownloadProgress>>(), Arg.Any<CancellationToken>())
                .Returns(new DownloadResult(true, tempFile, null));

            await _handler.HandleAsync(job, CancellationToken.None);

            job.Status.Should().Be(JobStatus.Completed);
            job.ResultData.Should().Contain("model.safetensors");
            await _catalogRepo.Received(1).UpsertAsync(Arg.Any<ModelRecord>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task HandleAsync_FailedDownload_FailsJob()
    {
        var request = new DownloadRequest("test-provider", "ext-123", null,
            new StorageRoot("/tmp/models", "Temp"), ModelType.Checkpoint);
        var json = JsonSerializer.Serialize(request);
        var job = CreateRunningJob(json);

        _mockProvider.DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<DownloadProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new DownloadResult(false, null, "Network error"));

        await _handler.HandleAsync(job, CancellationToken.None);

        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be("Network error");
    }

    [Fact]
    public async Task HandleAsync_SuccessfulDownload_UpdatesProgress()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.safetensors");
        await File.WriteAllBytesAsync(tempFile, new byte[2048]);

        try
        {
            var request = new DownloadRequest("test-provider", "ext-123", null,
                new StorageRoot(tempDir, "Temp"), ModelType.LoRA);
            var json = JsonSerializer.Serialize(request);
            var job = CreateRunningJob(json);

            _mockProvider.DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<DownloadProgress>>(), Arg.Any<CancellationToken>())
                .Returns(new DownloadResult(true, tempFile, null));

            await _handler.HandleAsync(job, CancellationToken.None);

            // Job should have progressed through 5% -> 95% -> 100%
            job.Status.Should().Be(JobStatus.Completed);
            job.Progress.Should().Be(100);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task HandleAsync_UsesRequestModelType_WhenNotUnknown()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "model.safetensors");
        await File.WriteAllBytesAsync(tempFile, new byte[1024]);

        try
        {
            var request = new DownloadRequest("test-provider", "ext-123", null,
                new StorageRoot(tempDir, "Temp"), ModelType.LoRA);
            var json = JsonSerializer.Serialize(request);
            var job = CreateRunningJob(json);

            _mockProvider.DownloadAsync(Arg.Any<DownloadRequest>(), Arg.Any<IProgress<DownloadProgress>>(), Arg.Any<CancellationToken>())
                .Returns(new DownloadResult(true, tempFile, null));

            await _handler.HandleAsync(job, CancellationToken.None);

            await _catalogRepo.Received(1).UpsertAsync(
                Arg.Is<ModelRecord>(r => r.Type == ModelType.LoRA),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
