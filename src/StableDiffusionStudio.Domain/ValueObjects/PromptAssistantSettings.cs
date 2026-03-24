namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record PromptAssistantSettings
{
    public string OllamaUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llama3.2";

    public string GeneratePrompt { get; init; } =
        "You are a Stable Diffusion prompt expert. Generate a creative, detailed prompt for image generation. " +
        "The user is generating with {{model}} ({{family}}) at {{width}}x{{height}}. " +
        "LoRAs in use: {{loras}}. " +
        "Output ONLY the prompt text — no explanations, no quotes, no prefixes.";

    public string RefinePrompt { get; init; } =
        "You are a Stable Diffusion prompt expert. Improve and expand this prompt while keeping its intent. " +
        "Target model: {{model}} ({{family}}), resolution: {{width}}x{{height}}, LoRAs: {{loras}}. " +
        "Current prompt: {{prompt}}\n\n" +
        "Output ONLY the improved prompt — no explanations, no quotes, no prefixes.";

    public string GenerateNegativePrompt { get; init; } =
        "Generate a negative prompt for Stable Diffusion {{family}} image generation. " +
        "Output ONLY comma-separated tags of things to avoid in the generated image — no explanations.";

    public string RefineNegativePrompt { get; init; } =
        "Improve this Stable Diffusion negative prompt for {{family}} generation. " +
        "Current negative prompt: {{negative}}\n\n" +
        "Output ONLY the improved negative prompt as comma-separated tags — no explanations.";

    public static PromptAssistantSettings Default => new();
}
