using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record GenerationParameters
{
    public required string PositivePrompt { get; init; }
    public string NegativePrompt { get; init; } = string.Empty;
    public required Guid CheckpointModelId { get; init; }
    public Guid? VaeModelId { get; init; }
    public IReadOnlyList<LoraReference> Loras { get; init; } = [];
    public Sampler Sampler { get; init; } = Sampler.EulerA;
    public Scheduler Scheduler { get; init; } = Scheduler.Normal;
    public int Steps { get; init; } = 20;
    public double CfgScale { get; init; } = 7.0;
    public long Seed { get; init; } = -1;
    public int Width { get; init; } = 512;
    public int Height { get; init; } = 512;
    public int BatchSize { get; init; } = 1;
    public int ClipSkip { get; init; } = 1;
    public int BatchCount { get; init; } = 1;
    public double Eta { get; init; } = 0.0;
    public double DenoisingStrength { get; init; } = 1.0;
}
