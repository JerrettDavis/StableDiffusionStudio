namespace StableDiffusionStudio.Domain.Services;

/// <summary>
/// Estimates CLIP token count for a prompt string.
/// CLIP tokenizer averages ~1.3 tokens per word.
/// SD 1.5 limit: 77 tokens, SDXL/Flux: 154 tokens (2x77).
/// </summary>
public static class PromptTokenEstimator
{
    private static readonly char[] Separators = [' ', ',', '.', '\n', '\r', '\t'];

    public static int EstimateTokens(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return 0;

        var words = prompt.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        return (int)(words.Length * 1.3);
    }

    public static int GetTokenLimit(string? modelFamily)
    {
        return modelFamily switch
        {
            "SDXL" or "Flux" => 154, // 2x77
            _ => 77 // SD 1.5 default
        };
    }
}
