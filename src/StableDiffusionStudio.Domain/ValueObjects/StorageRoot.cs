namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record StorageRoot
{
    public string Path { get; }
    public string DisplayName { get; }

    public StorageRoot(string path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Storage root path is required.", nameof(path));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        Path = path.Trim();
        DisplayName = displayName.Trim();
    }
}
