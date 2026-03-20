using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Infrastructure.Jobs;

namespace StableDiffusionStudio.Infrastructure.Tests.Jobs;

public class ModelScanJobHandlerTests
{
    private readonly IModelCatalogService _catalogService = Substitute.For<IModelCatalogService>();
    private readonly ILogger<ModelScanJobHandler> _logger = Substitute.For<ILogger<ModelScanJobHandler>>();
    private readonly ModelScanJobHandler _handler;

    public ModelScanJobHandlerTests()
    {
        _handler = new ModelScanJobHandler(_catalogService, _logger);
    }

    [Fact]
    public async Task HandleAsync_CallsScanAsyncOnService()
    {
        var job = JobRecord.Create("model-scan", "/models");
        job.Start();
        _catalogService.ScanAsync(Arg.Any<ScanModelsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ScanResult(3, 1, 0));

        await _handler.HandleAsync(job, CancellationToken.None);

        await _catalogService.Received(1).ScanAsync(
            Arg.Is<ScanModelsCommand>(c => c.StorageRootPath == "/models"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_CompletesJobWithResultData()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();
        _catalogService.ScanAsync(Arg.Any<ScanModelsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ScanResult(5, 2, 1));

        await _handler.HandleAsync(job, CancellationToken.None);

        job.Status.Should().Be(Domain.Enums.JobStatus.Completed);
        job.Progress.Should().Be(100);
        job.ResultData.Should().Contain("New: 5");
        job.ResultData.Should().Contain("Updated: 2");
        job.ResultData.Should().Contain("Missing: 1");
    }

    [Fact]
    public async Task HandleAsync_UpdatesProgressDuringScan()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();
        _catalogService.ScanAsync(Arg.Any<ScanModelsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ScanResult(0, 0, 0));

        await _handler.HandleAsync(job, CancellationToken.None);

        // After completion, progress should be 100
        job.Progress.Should().Be(100);
        job.Phase.Should().Be("Scan complete");
    }

    [Fact]
    public async Task HandleAsync_WithNullData_PassesNullToScanCommand()
    {
        var job = JobRecord.Create("model-scan");
        job.Start();
        _catalogService.ScanAsync(Arg.Any<ScanModelsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ScanResult(0, 0, 0));

        await _handler.HandleAsync(job, CancellationToken.None);

        await _catalogService.Received(1).ScanAsync(
            Arg.Is<ScanModelsCommand>(c => c.StorageRootPath == null),
            Arg.Any<CancellationToken>());
    }
}
