namespace StableDiffusionStudio.Application.Interfaces;

/// <summary>
/// Generates text prompts from images using a vision model (e.g. Ollama + llava/llama3.2-vision).
/// </summary>
public interface IImageInterrogator
{
    Task<string> InterrogateAsync(byte[] imageBytes, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
