using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class ExperimentRun
{
    public Guid Id { get; private set; }
    public Guid ExperimentId { get; private set; }
    public long FixedSeed { get; private set; }
    public bool UseFixedSeed { get; private set; } = true;
    public int TotalCombinations { get; private set; }
    public int CompletedCount { get; private set; }
    public ExperimentRunStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    private readonly List<ExperimentRunImage> _images = [];
    public IReadOnlyList<ExperimentRunImage> Images => _images.AsReadOnly();

    private ExperimentRun() { } // EF Core

    public static ExperimentRun Create(
        Guid experimentId,
        int totalCombinations,
        long fixedSeed,
        bool useFixedSeed = true)
    {
        if (totalCombinations < 1)
            throw new ArgumentException("Total combinations must be at least 1.", nameof(totalCombinations));

        return new ExperimentRun
        {
            Id = Guid.NewGuid(),
            ExperimentId = experimentId,
            TotalCombinations = totalCombinations,
            FixedSeed = fixedSeed,
            UseFixedSeed = useFixedSeed,
            Status = ExperimentRunStatus.Pending
        };
    }

    public void Start()
    {
        Status = ExperimentRunStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void Complete()
    {
        Status = ExperimentRunStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string error)
    {
        Status = ExperimentRunStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        ErrorMessage = error;
    }

    public void Cancel()
    {
        Status = ExperimentRunStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementCompleted() => CompletedCount++;

    public void AddImage(ExperimentRunImage image) => _images.Add(image);
}
