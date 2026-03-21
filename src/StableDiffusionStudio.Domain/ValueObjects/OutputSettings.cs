namespace StableDiffusionStudio.Domain.ValueObjects;

/// <summary>
/// Configures how generated images are saved to disk — directory, filename pattern, and auto-save behavior.
/// </summary>
public sealed record OutputSettings
{
    public string? CustomOutputDirectory { get; init; }
    public string FilenamePattern { get; init; } = "[seed]";
    public bool AutoSave { get; init; } = true;
    public bool SaveGrid { get; init; }

    public static OutputSettings Default => new();

    public string FormatFilename(long seed, string prompt, DateTimeOffset date, string modelName)
    {
        var truncatedPrompt = prompt.Length > 50 ? prompt[..50] : prompt;
        var result = FilenamePattern
            .Replace("[seed]", seed.ToString())
            .Replace("[prompt]", SanitizeFilename(truncatedPrompt))
            .Replace("[date]", date.ToString("yyyyMMdd-HHmmss"))
            .Replace("[model]", SanitizeFilename(modelName));
        return result + ".png";
    }

    private static readonly char[] InvalidChars =
        Path.GetInvalidFileNameChars()
            .Union(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
            .Distinct()
            .ToArray();

    private static string SanitizeFilename(string name)
        => string.Join("_", name.Split(InvalidChars));
}
