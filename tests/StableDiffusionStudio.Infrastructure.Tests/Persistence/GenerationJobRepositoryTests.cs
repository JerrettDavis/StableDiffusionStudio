using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public class GenerationJobRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly GenerationJobRepository _repo;

    private static GenerationParameters ValidParameters => new()
    {
        PositivePrompt = "a beautiful landscape",
        CheckpointModelId = Guid.NewGuid(),
        NegativePrompt = "ugly, blurry",
        Steps = 25,
        CfgScale = 7.5,
        Width = 512,
        Height = 768,
        Sampler = Sampler.DPMPlusPlus2MKarras,
        Scheduler = Scheduler.Karras,
        Seed = 42,
        BatchSize = 2
    };

    public GenerationJobRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new GenerationJobRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsJobWithParameters()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        await _repo.AddAsync(job);

        var retrieved = await _repo.GetByIdAsync(job.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(job.Id);
        retrieved.Parameters.PositivePrompt.Should().Be("a beautiful landscape");
        retrieved.Parameters.NegativePrompt.Should().Be("ugly, blurry");
        retrieved.Parameters.Steps.Should().Be(25);
        retrieved.Parameters.CfgScale.Should().Be(7.5);
        retrieved.Parameters.Width.Should().Be(512);
        retrieved.Parameters.Height.Should().Be(768);
        retrieved.Parameters.Sampler.Should().Be(Sampler.DPMPlusPlus2MKarras);
        retrieved.Parameters.Scheduler.Should().Be(Scheduler.Karras);
        retrieved.Parameters.Seed.Should().Be(42);
        retrieved.Parameters.BatchSize.Should().Be(2);
        retrieved.Status.Should().Be(GenerationJobStatus.Pending);
    }

    [Fact]
    public async Task AddAsync_ThenGetById_IncludesImages()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        var image = GeneratedImage.Create(job.Id, "/images/test.png", 42, 512, 768, 1.5, "{}");
        job.AddImage(image);
        await _repo.AddAsync(job);

        var retrieved = await _repo.GetByIdAsync(job.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Images.Should().HaveCount(1);
        retrieved.Images[0].FilePath.Should().Be("/images/test.png");
        retrieved.Images[0].Seed.Should().Be(42);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListByProjectAsync_ReturnsJobsForProject()
    {
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        await _repo.AddAsync(GenerationJob.Create(projectId, ValidParameters));
        await _repo.AddAsync(GenerationJob.Create(projectId, ValidParameters));
        await _repo.AddAsync(GenerationJob.Create(otherProjectId, ValidParameters));

        var results = await _repo.ListByProjectAsync(projectId);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(j => j.ProjectId.Should().Be(projectId));
    }

    [Fact]
    public async Task ListByProjectAsync_OrdersByCreatedAtDesc()
    {
        var projectId = Guid.NewGuid();
        var job1 = GenerationJob.Create(projectId, ValidParameters);
        await _repo.AddAsync(job1);
        await Task.Delay(10);
        var job2 = GenerationJob.Create(projectId, ValidParameters);
        await _repo.AddAsync(job2);

        var results = await _repo.ListByProjectAsync(projectId);

        results[0].Id.Should().Be(job2.Id);
        results[1].Id.Should().Be(job1.Id);
    }

    [Fact]
    public async Task ListByProjectAsync_SupportsPagination()
    {
        var projectId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            await _repo.AddAsync(GenerationJob.Create(projectId, ValidParameters));
            await Task.Delay(10);
        }

        var results = await _repo.ListByProjectAsync(projectId, skip: 2, take: 2);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatusChanges()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        await _repo.AddAsync(job);

        job.Start();
        job.Complete();
        await _repo.UpdateAsync(job);

        var retrieved = await _repo.GetByIdAsync(job.Id);
        retrieved!.Status.Should().Be(GenerationJobStatus.Completed);
        retrieved.StartedAt.Should().NotBeNull();
        retrieved.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsErrorMessage()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        await _repo.AddAsync(job);

        job.Start();
        job.Fail("Something went wrong");
        await _repo.UpdateAsync(job);

        var retrieved = await _repo.GetByIdAsync(job.Id);
        retrieved!.Status.Should().Be(GenerationJobStatus.Failed);
        retrieved.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task UpdateAsync_PersistsNewImages()
    {
        var job = GenerationJob.Create(Guid.NewGuid(), ValidParameters);
        await _repo.AddAsync(job);

        var image = GeneratedImage.Create(job.Id, "/images/new.png", 99, 512, 512, 2.0, "{}");
        job.AddImage(image);
        await _repo.UpdateAsync(job);

        var retrieved = await _repo.GetByIdAsync(job.Id);
        retrieved!.Images.Should().HaveCount(1);
        retrieved.Images[0].Seed.Should().Be(99);
    }

    [Fact]
    public async Task ParametersWithLoras_RoundTripsCorrectly()
    {
        var parameters = ValidParameters with
        {
            Loras = [new LoraReference(Guid.NewGuid(), 0.8), new LoraReference(Guid.NewGuid(), 1.2)]
        };
        var job = GenerationJob.Create(Guid.NewGuid(), parameters);
        await _repo.AddAsync(job);

        var retrieved = await _repo.GetByIdAsync(job.Id);

        retrieved!.Parameters.Loras.Should().HaveCount(2);
        retrieved.Parameters.Loras[0].Weight.Should().Be(0.8);
        retrieved.Parameters.Loras[1].Weight.Should().Be(1.2);
    }
}
