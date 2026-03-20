using FluentAssertions;
using NSubstitute;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.DTOs;
using StableDiffusionStudio.Application.Interfaces;
using StableDiffusionStudio.Application.Services;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Application.Tests.Services;

public class ProjectServiceTests
{
    private readonly IProjectRepository _repo = Substitute.For<IProjectRepository>();
    private readonly ProjectService _service;

    public ProjectServiceTests() { _service = new ProjectService(_repo); }

    [Fact]
    public async Task CreateAsync_WithValidCommand_ReturnsProjectDto()
    {
        var command = new CreateProjectCommand("Test Project", "Description");
        var result = await _service.CreateAsync(command);
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Project");
        result.Description.Should().Be("Description");
        result.Status.Should().Be(ProjectStatus.Active);
        await _repo.Received(1).AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_WithValidCommand_RenamesProject()
    {
        var project = Project.Create("Old Name", null);
        _repo.GetByIdAsync(project.Id).Returns(project);
        var command = new RenameProjectCommand(project.Id, "New Name");
        await _service.RenameAsync(command);
        project.Name.Should().Be("New Name");
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_ProjectNotFound_ThrowsKeyNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);
        var act = () => _service.RenameAsync(new RenameProjectCommand(Guid.NewGuid(), "Name"));
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ArchiveAsync_ArchivesProject()
    {
        var project = Project.Create("Test", null);
        _repo.GetByIdAsync(project.Id).Returns(project);
        await _service.ArchiveAsync(project.Id);
        project.Status.Should().Be(ProjectStatus.Archived);
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_DelegatesToRepository()
    {
        var filter = new ProjectFilter();
        var projects = new List<Project> { Project.Create("A", null), Project.Create("B", null) };
        _repo.ListAsync(filter).Returns(projects);
        var results = await _service.ListAsync(filter);
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToRepository()
    {
        var id = Guid.NewGuid();
        await _service.DeleteAsync(id);
        await _repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PinAsync_PinsProject()
    {
        var project = Project.Create("Test", null);
        _repo.GetByIdAsync(project.Id).Returns(project);
        await _service.PinAsync(project.Id);
        project.IsPinned.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RestoreAsync_RestoresArchivedProject()
    {
        var project = Project.Create("Test", null);
        project.Archive();
        _repo.GetByIdAsync(project.Id).Returns(project);

        await _service.RestoreAsync(project.Id);

        project.Status.Should().Be(ProjectStatus.Active);
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnpinAsync_UnpinsProject()
    {
        var project = Project.Create("Test", null);
        project.Pin();
        _repo.GetByIdAsync(project.Id).Returns(project);

        await _service.UnpinAsync(project.Id);

        project.IsPinned.Should().BeFalse();
        await _repo.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsDto()
    {
        var project = Project.Create("Test Project", "Description");
        _repo.GetByIdAsync(project.Id).Returns(project);

        var result = await _service.GetByIdAsync(project.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(project.Id);
        result.Name.Should().Be("Test Project");
        result.Description.Should().Be("Description");
        result.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var result = await _service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveAsync_WhenProjectNotFound_ThrowsKeyNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var act = () => _service.ArchiveAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RestoreAsync_WhenProjectNotFound_ThrowsKeyNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var act = () => _service.RestoreAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
