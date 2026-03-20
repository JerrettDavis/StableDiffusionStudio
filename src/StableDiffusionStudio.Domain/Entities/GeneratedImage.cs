namespace StableDiffusionStudio.Domain.Entities;

public class GeneratedImage
{
    public Guid Id { get; private set; }
    public Guid GenerationJobId { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public long Seed { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public double GenerationTimeSeconds { get; private set; }
    public string ParametersJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsFavorite { get; private set; }

    private GeneratedImage() { } // EF Core

    public static GeneratedImage Create(Guid generationJobId, string filePath, long seed,
        int width, int height, double generationTimeSeconds, string parametersJson)
    {
        return new GeneratedImage
        {
            Id = Guid.NewGuid(),
            GenerationJobId = generationJobId,
            FilePath = filePath,
            Seed = seed,
            Width = width,
            Height = height,
            GenerationTimeSeconds = generationTimeSeconds,
            ParametersJson = parametersJson,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
    }
}
