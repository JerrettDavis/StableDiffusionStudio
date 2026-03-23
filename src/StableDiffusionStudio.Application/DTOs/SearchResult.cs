namespace StableDiffusionStudio.Application.DTOs;

public record SearchResult(IReadOnlyList<RemoteModelInfo> Models, int TotalCount, bool HasMore, string? NextCursor = null);
