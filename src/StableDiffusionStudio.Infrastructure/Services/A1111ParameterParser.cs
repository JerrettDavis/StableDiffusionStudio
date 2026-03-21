namespace StableDiffusionStudio.Infrastructure.Services;

/// <summary>
/// Parses A1111-compatible parameter strings from PNG metadata.
/// Format: "prompt\nNegative prompt: neg\nSteps: 20, Sampler: Euler a, CFG scale: 7, Seed: 12345, Size: 512x512, Model: name"
/// </summary>
public static class A1111ParameterParser
{
    public record ParsedParameters(
        string PositivePrompt,
        string NegativePrompt,
        int? Steps,
        string? Sampler,
        double? CfgScale,
        long? Seed,
        int? Width,
        int? Height,
        string? Model,
        int? ClipSkip);

    public static ParsedParameters? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var lines = text.Split('\n');
        var positive = new List<string>();
        var negative = "";
        var paramLine = "";

        var inNegative = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("Negative prompt:"))
            {
                negative = line["Negative prompt:".Length..].Trim();
                inNegative = true;
            }
            else if (line.StartsWith("Steps:"))
            {
                paramLine = line;
                inNegative = false;
            }
            else if (inNegative)
            {
                negative += " " + line.Trim();
            }
            else
            {
                positive.Add(line);
            }
        }

        var positivePrompt = string.Join("\n", positive).Trim();

        // Parse key-value pairs from param line
        var kvPairs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(paramLine))
        {
            foreach (var part in paramLine.Split(','))
            {
                var kv = part.Split(':', 2);
                if (kv.Length == 2)
                    kvPairs[kv[0].Trim()] = kv[1].Trim();
            }
        }

        return new ParsedParameters(
            positivePrompt,
            negative,
            kvPairs.TryGetValue("Steps", out var s) && int.TryParse(s, out var steps) ? steps : null,
            kvPairs.GetValueOrDefault("Sampler"),
            kvPairs.TryGetValue("CFG scale", out var c) && double.TryParse(c, System.Globalization.CultureInfo.InvariantCulture, out var cfg) ? cfg : null,
            kvPairs.TryGetValue("Seed", out var sd) && long.TryParse(sd, out var seed) ? seed : null,
            ParseDimension(kvPairs.GetValueOrDefault("Size"), 0),
            ParseDimension(kvPairs.GetValueOrDefault("Size"), 1),
            kvPairs.GetValueOrDefault("Model"),
            kvPairs.TryGetValue("Clip skip", out var cs) && int.TryParse(cs, out var clipSkip) ? clipSkip : null
        );
    }

    private static int? ParseDimension(string? size, int index)
    {
        if (size is null) return null;
        var parts = size.Split('x');
        if (parts.Length > index && int.TryParse(parts[index], out var val)) return val;
        return null;
    }
}
