namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record FileLocation
{
    public string Path { get; }

    public FileLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path is required.", nameof(path));
        Path = path;
    }
}
