using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Entities;

public class GenerationPresetEntity
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? AssociatedModelId { get; private set; }  // null = universal preset
    public ModelFamily? ModelFamilyFilter { get; private set; }  // null = any family
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // All generation parameters (excluding prompt and model — those are workspace state)
    public string? PositivePromptTemplate { get; private set; }
    public string NegativePrompt { get; private set; } = string.Empty;
    public Sampler Sampler { get; private set; } = Sampler.EulerA;
    public Scheduler Scheduler { get; private set; } = Scheduler.Normal;
    public int Steps { get; private set; } = 20;
    public double CfgScale { get; private set; } = 7.0;
    public int Width { get; private set; } = 512;
    public int Height { get; private set; } = 512;
    public int BatchSize { get; private set; } = 1;
    public int ClipSkip { get; private set; } = 1;

    private GenerationPresetEntity() { } // EF Core

    public static GenerationPresetEntity Create(
        string name, string? description,
        Guid? associatedModelId, ModelFamily? modelFamilyFilter,
        string? positivePromptTemplate, string negativePrompt,
        Sampler sampler, Scheduler scheduler,
        int steps, double cfgScale, int width, int height,
        int batchSize, int clipSkip)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Preset name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;
        return new GenerationPresetEntity
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description,
            AssociatedModelId = associatedModelId,
            ModelFamilyFilter = modelFamilyFilter,
            PositivePromptTemplate = positivePromptTemplate,
            NegativePrompt = negativePrompt,
            Sampler = sampler,
            Scheduler = scheduler,
            Steps = steps,
            CfgScale = cfgScale,
            Width = width,
            Height = height,
            BatchSize = batchSize,
            ClipSkip = clipSkip,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string name, string? description,
        Guid? associatedModelId, ModelFamily? modelFamilyFilter,
        string? positivePromptTemplate, string negativePrompt,
        Sampler sampler, Scheduler scheduler,
        int steps, double cfgScale, int width, int height,
        int batchSize, int clipSkip)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Preset name is required.", nameof(name));

        Name = name.Trim();
        Description = description;
        AssociatedModelId = associatedModelId;
        ModelFamilyFilter = modelFamilyFilter;
        PositivePromptTemplate = positivePromptTemplate;
        NegativePrompt = negativePrompt;
        Sampler = sampler;
        Scheduler = scheduler;
        Steps = steps;
        CfgScale = cfgScale;
        Width = width;
        Height = height;
        BatchSize = batchSize;
        ClipSkip = clipSkip;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
