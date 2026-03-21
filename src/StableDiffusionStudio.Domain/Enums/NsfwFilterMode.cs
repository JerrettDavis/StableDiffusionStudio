namespace StableDiffusionStudio.Domain.Enums;

public enum NsfwFilterMode
{
    Off,            // No filtering — show everything
    Blur,           // Blur NSFW content, user can reveal per-image
    BlockAndDelete, // Auto-delete NSFW content on detection
}
