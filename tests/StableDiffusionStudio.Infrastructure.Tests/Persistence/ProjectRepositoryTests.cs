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

    [Fact]
    public async Task ListAsync_OrdersPinnedFirst()
    {
        var unpinned = Project.Create("Unpinned", null);
        var pinned = Project.Create("Pinned", null);
        pinned.Pin();
        await _repo.AddAsync(unpinned);
        await _repo.AddAsync(pinned);

        var results = await _repo.ListAsync(new ProjectFilter());

        results[0].Name.Should().Be("Pinned");
    }

    [Fact]
    public async Task ListAsync_WithPagination_SkipAndTake()
    {
        for (int i = 0; i < 10; i++)
            await _repo.AddAsync(Project.Create($"Project {i}", null));

        var results = await _repo.ListAsync(new ProjectFilter(Skip: 2, Take: 3));

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNotThrow()
    {
        var act = () => _repo.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_ChangesPersistAcrossQueries()
    {
        var project = Project.Create("Original Name", "Original Desc");
        await _repo.AddAsync(project);

        project.Rename("New Name");
        await _repo.UpdateAsync(project);

        _context.ChangeTracker.Clear();
        var retrieved = await _repo.GetByIdAsync(project.Id);
        retrieved!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_NoFilter_ReturnsAll()
    {
        await _repo.AddAsync(Project.Create("A", null));
        await _repo.AddAsync(Project.Create("B", null));
        await _repo.AddAsync(Project.Create("C", null));

        var results = await _repo.ListAsync(new ProjectFilter());

        results.Should().HaveCount(3);
    }
}
