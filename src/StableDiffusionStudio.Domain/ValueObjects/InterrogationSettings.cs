namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record InterrogationSettings
{
    public string OllamaUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llava";
    public string SystemPrompt { get; init; } = "You are a Stable Diffusion prompt writer. Describe the image in a comma-separated list of tags suitable for image generation. Focus on: subject, composition, lighting, style, colors, medium, artist style. Be concise — output ONLY the comma-separated tags, no sentences or explanations.";

    public static InterrogationSettings Default => new();
}
