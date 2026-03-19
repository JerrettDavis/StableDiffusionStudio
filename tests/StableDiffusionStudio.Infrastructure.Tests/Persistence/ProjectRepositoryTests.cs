using FluentAssertions;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Infrastructure.Persistence.Repositories;

namespace StableDiffusionStudio.Infrastructure.Tests.Persistence;

public class ProjectRepositoryTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly Infrastructure.Persistence.AppDbContext _context;
    private readonly ProjectRepository _repo;

    public ProjectRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.Create();
        _repo = new ProjectRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsProject()
    {
        var project = Project.Create("Test Project", "Description");
        await _repo.AddAsync(project);
        var retrieved = await _repo.GetByIdAsync(project.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Project");
        retrieved.Description.Should().Be("Description");
    }

    [Fact]
    public async Task ListAsync_WithSearchTerm_FiltersResults()
    {
        await _repo.AddAsync(Project.Create("Alpha Project", null));
        await _repo.AddAsync(Project.Create("Beta Project", null));
        await _repo.AddAsync(Project.Create("Gamma", null));
        var results = await _repo.ListAsync(new ProjectFilter(SearchTerm: "Project"));
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_WithStatusFilter_FiltersResults()
    {
        var active = Project.Create("Active", null);
        var archived = Project.Create("Archived", null);
        archived.Archive();
        await _repo.AddAsync(active);
        await _repo.AddAsync(archived);
        var results = await _repo.ListAsync(new ProjectFilter(Status: ProjectStatus.Archived));
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Archived");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var project = Project.Create("Original", null);
        await _repo.AddAsync(project);
        project.Rename("Updated");
        await _repo.UpdateAsync(project);
        var retrieved = await _repo.GetByIdAsync(project.Id);
        retrieved!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task DeleteAsync_RemovesProject()
    {
        var project = Project.Create("To Delete", null);
        await _repo.AddAsync(project);
        await _repo.DeleteAsync(project.Id);
        var retrieved = await _repo.GetByIdAsync(project.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_WithPinnedFilter_FiltersResults()
    {
        var pinned = Project.Create("Pinned", null);
        pinned.Pin();
        var unpinned = Project.Create("Unpinned", null);
        await _repo.AddAsync(pinned);
        await _repo.AddAsync(unpinned);
        var results = await _repo.ListAsync(new ProjectFilter(IsPinned: true));
        results.Should().HaveCount(1);
        results[0].Name.Should().Be("Pinned");
    }
}
