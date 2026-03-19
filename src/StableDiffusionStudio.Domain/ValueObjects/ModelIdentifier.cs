namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record ModelIdentifier(string Source, string ExternalId)
{
    public string Source { get; } = !string.IsNullOrWhiteSpace(Source)
        ? Source : throw new ArgumentException("Source is required.", nameof(Source));
    public string ExternalId { get; } = !string.IsNullOrWhiteSpace(ExternalId)
        ? ExternalId : throw new ArgumentException("ExternalId is required.", nameof(ExternalId));
}
