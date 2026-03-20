namespace StableDiffusionStudio.Domain.Entities;

public class PromptHistory
{
    public Guid Id { get; private set; }
    public string PositivePrompt { get; private set; } = string.Empty;
    public string NegativePrompt { get; private set; } = string.Empty;
    public DateTimeOffset UsedAt { get; private set; }
    public int UseCount { get; private set; }

    private PromptHistory() { }

    public static PromptHistory Create(string positivePrompt, string negativePrompt)
    {
        if (string.IsNullOrWhiteSpace(positivePrompt))
            throw new ArgumentException("Positive prompt is required.", nameof(positivePrompt));

        return new PromptHistory
        {
            Id = Guid.NewGuid(),
            PositivePrompt = positivePrompt,
            NegativePrompt = negativePrompt ?? string.Empty,
            UsedAt = DateTimeOffset.UtcNow,
            UseCount = 1
        };
    }

    public void IncrementUsage()
    {
        UseCount++;
        UsedAt = DateTimeOffset.UtcNow;
    }
}
