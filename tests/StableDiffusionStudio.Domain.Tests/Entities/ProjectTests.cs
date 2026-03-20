using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ProjectTests
{
    [Fact]
    public void Create_WithValidName_SetsPropertiesCorrectly()
    {
        var project = Project.Create("My Project", "A description");

        project.Id.Should().NotBeEmpty();
        project.Name.Should().Be("My Project");
        project.Description.Should().Be("A description");
        project.Status.Should().Be(ProjectStatus.Active);
        project.IsPinned.Should().BeFalse();
        project.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        var act = () => Project.Create(name!, null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rename_WhenActive_UpdatesName()
    {
        var project = Project.Create("Old Name", null);
        project.Rename("New Name");
        project.Name.Should().Be("New Name");
        project.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Rename_WhenArchived_ThrowsInvalidOperationException()
    {
        var project = Project.Create("Test", null);
        project.Archive();
        var act = () => project.Rename("New Name");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        var project = Project.Create("Test", null);
        project.Archive();
        project.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public void Restore_FromArchived_SetsStatusToActive()
    {
        var project = Project.Create("Test", null);
        project.Archive();
        project.Restore();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void Pin_SetsIsPinnedTrue()
    {
        var project = Project.Create("Test", null);
        project.Pin();
        project.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Unpin_SetsIsPinnedFalse()
    {
        var project = Project.Create("Test", null);
        project.Pin();
        project.Unpin();
        project.IsPinned.Should().BeFalse();
    }

    [Fact]
    public void UpdateDescription_SetsDescription()
    {
        var project = Project.Create("Test", null);
        project.UpdateDescription("New description");
        project.Description.Should().Be("New description");
    }

    [Fact]
    public void Archive_AlreadyArchived_IsIdempotent()
    {
        var project = Project.Create("Test", null);
        project.Archive();
        var firstUpdatedAt = project.UpdatedAt;

        project.Archive();

        project.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public void Restore_ActiveProject_RemainsActive()
    {
        var project = Project.Create("Test", null);
        project.Restore();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithInvalidName_ThrowsArgumentException(string? name)
    {
        var project = Project.Create("Test", null);
        var act = () => project.Rename(name!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Pin_AlreadyPinned_IsIdempotent()
    {
        var project = Project.Create("Test", null);
        project.Pin();
        project.Pin();
        project.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void UpdateDescription_ToNull_ClearsDescription()
    {
        var project = Project.Create("Test", "Some description");
        project.UpdateDescription(null);
        project.Description.Should().BeNull();
    }

    [Fact]
    public void Rename_TrimsWhitespace()
    {
        var project = Project.Create("Test", null);
        project.Rename("  Trimmed Name  ");
        project.Name.Should().Be("Trimmed Name");
    }

    [Fact]
    public void Create_TrimsName()
    {
        var project = Project.Create("  Padded Name  ", null);
        project.Name.Should().Be("Padded Name");
    }
}
