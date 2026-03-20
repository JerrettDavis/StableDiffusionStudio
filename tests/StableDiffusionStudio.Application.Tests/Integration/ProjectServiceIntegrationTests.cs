using FluentAssertions;
using Microsoft.Data.Sqlite;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Application.Tests.Integration;

public class ProjectServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ProjectService _service;

    public ProjectServiceIntegrationTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        var repo = new ProjectRepository(_context);
        _service = new ProjectService(repo);
    }

    [Fact]
    public async Task CreateAsync_PersistsToDatabase()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Test Project", "A description"));

        dto.Id.Should().NotBeEmpty();
        dto.Name.Should().Be("Test Project");
        dto.Description.Should().Be("A description");
        dto.Status.Should().Be(ProjectStatus.Active);
        dto.IsPinned.Should().BeFalse();

        // Verify it persisted
        var fromDb = await _service.GetByIdAsync(dto.Id);
        fromDb.Should().NotBeNull();
        fromDb!.Name.Should().Be("Test Project");
    }

    [Fact]
    public async Task RenameAsync_UpdatesInDatabase()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Original", null));

        await _service.RenameAsync(new RenameProjectCommand(dto.Id, "Renamed"));

        var fromDb = await _service.GetByIdAsync(dto.Id);
        fromDb!.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task ArchiveAsync_SetsStatusInDatabase()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Archivable", null));

        await _service.ArchiveAsync(dto.Id);

        var fromDb = await _service.GetByIdAsync(dto.Id);
        fromDb!.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public async Task RestoreAsync_SetsStatusBackToActive()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Restorable", null));
        await _service.ArchiveAsync(dto.Id);

        await _service.RestoreAsync(dto.Id);

        var fromDb = await _service.GetByIdAsync(dto.Id);
        fromDb!.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromDatabase()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Deletable", null));

        await _service.DeleteAsync(dto.Id);

        var fromDb = await _service.GetByIdAsync(dto.Id);
        fromDb.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllProjects()
    {
        await _service.CreateAsync(new CreateProjectCommand("Project A", null));
        await _service.CreateAsync(new CreateProjectCommand("Project B", null));
        await _service.CreateAsync(new CreateProjectCommand("Project C", null));

        var list = await _service.ListAsync(new ProjectFilter());

        list.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListAsync_FiltersBySearchTerm()
    {
        await _service.CreateAsync(new CreateProjectCommand("Landscape Shots", null));
        await _service.CreateAsync(new CreateProjectCommand("Portrait Work", null));
        await _service.CreateAsync(new CreateProjectCommand("Landscape Panoramas", null));

        var list = await _service.ListAsync(new ProjectFilter(SearchTerm: "Landscape"));

        list.Should().HaveCount(2);
        list.Should().OnlyContain(p => p.Name.Contains("Landscape"));
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        var active = await _service.CreateAsync(new CreateProjectCommand("Active One", null));
        var archived = await _service.CreateAsync(new CreateProjectCommand("Archived One", null));
        await _service.ArchiveAsync(archived.Id);

        var list = await _service.ListAsync(new ProjectFilter(Status: ProjectStatus.Archived));

        list.Should().HaveCount(1);
        list[0].Name.Should().Be("Archived One");
    }

    [Fact]
    public async Task PinAsync_PersistsInDatabase()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Pinnable", null));

        await _service.PinAsync(dto.Id);

        var fromDb = await _service.GetByIdAsync(dto.Id);
        fromDb!.IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task UnpinAsync_PersistsInDatabase()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Unpinnable", null));
        await _service.PinAsync(dto.Id);

        await _service.UnpinAsync(dto.Id);

        var fromDb = await _service.GetByIdAsync(dto.Id);
        fromDb!.IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectDto()
    {
        var dto = await _service.CreateAsync(new CreateProjectCommand("Specific", "Desc"));

        var fromDb = await _service.GetByIdAsync(dto.Id);

        fromDb.Should().NotBeNull();
        fromDb!.Id.Should().Be(dto.Id);
        fromDb.Name.Should().Be("Specific");
        fromDb.Description.Should().Be("Desc");
        fromDb.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var fromDb = await _service.GetByIdAsync(Guid.NewGuid());

        fromDb.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_FiltersByPinned()
    {
        var pinned = await _service.CreateAsync(new CreateProjectCommand("Pinned", null));
        await _service.PinAsync(pinned.Id);
        await _service.CreateAsync(new CreateProjectCommand("Not Pinned", null));

        var list = await _service.ListAsync(new ProjectFilter(IsPinned: true));

        list.Should().HaveCount(1);
        list[0].Name.Should().Be("Pinned");
    }

    [Fact]
    public async Task RenameAsync_NonExistent_ThrowsKeyNotFound()
    {
        var act = () => _service.RenameAsync(new RenameProjectCommand(Guid.NewGuid(), "New Name"));

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
