namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record LoraReference(Guid ModelId, double Weight = 1.0);
