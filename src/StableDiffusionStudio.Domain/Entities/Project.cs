using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class Project
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public ProjectStatus Status { get; private set; }
    public bool IsPinned { get; private set; }

    private Project() { } // EF Core

    public static Project Create(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        return new Project
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description,
            CreatedAt = now,
            UpdatedAt = now,
            Status = ProjectStatus.Active,
            IsPinned = false
        };
    }

    public void Rename(string newName)
    {
        if (Status == ProjectStatus.Archived)
            throw new InvalidOperationException("Cannot rename an archived project.");
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Project name is required.", nameof(newName));
        Name = newName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive() { Status = ProjectStatus.Archived; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Restore() { Status = ProjectStatus.Active; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Pin() { IsPinned = true; UpdatedAt = DateTimeOffset.UtcNow; }
    public void Unpin() { IsPinned = false; UpdatedAt = DateTimeOffset.UtcNow; }
    public void UpdateDescription(string? description) { Description = description; UpdatedAt = DateTimeOffset.UtcNow; }
}
