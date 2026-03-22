using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class ExperimentRunImage
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public long Seed { get; private set; }
    public double GenerationTimeSeconds { get; private set; }
    public string AxisValuesJson { get; private set; } = "{}";
    public int GridX { get; private set; }
    public int GridY { get; private set; }
    public bool IsWinner { get; private set; }
    public ContentRating ContentRating { get; private set; } = ContentRating.Unknown;
    public double NsfwScore { get; private set; }

    private ExperimentRunImage() { } // EF Core

    public static ExperimentRunImage Create(
        Guid runId,
        string filePath,
        long seed,
        double generationTimeSeconds,
        string axisValuesJson,
        int gridX,
        int gridY)
    {
        return new ExperimentRunImage
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            FilePath = filePath,
            Seed = seed,
            GenerationTimeSeconds = generationTimeSeconds,
            AxisValuesJson = axisValuesJson,
            GridX = gridX,
            GridY = gridY
        };
    }

    public void MarkAsWinner() => IsWinner = true;
    public void UnmarkAsWinner() => IsWinner = false;

    public void SetContentRating(ContentRating rating, double score)
    {
        ContentRating = rating;
        NsfwScore = score;
    }
}
