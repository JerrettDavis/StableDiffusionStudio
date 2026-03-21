namespace StableDiffusionStudio.Domain.Services;

/// <summary>
/// Parses prompts containing | delimiters and expands them into individual prompt variants.
/// Each pipe-separated segment becomes a separate generation target.
/// Example: "a cat | a dog | a bird" produces ["a cat", "a dog", "a bird"].
/// </summary>
public static class PromptMatrixParser
{
    public static IReadOnlyList<string> ExpandMatrix(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return [prompt];

        if (!prompt.Contains('|'))
            return [prompt];

        var segments = prompt.Split('|')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return segments.Count == 0 ? [prompt] : segments;
    }
}
