using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record DownloadLocationSettings
{
    public Dictionary<string, string> ProviderRoots { get; init; } = new();

    public static DownloadLocationSettings Default => new();

    public string GetDownloadPath(string providerId, ModelType modelType, string defaultRoot)
    {
        var root = ProviderRoots.TryGetValue(providerId, out var customRoot)
            ? customRoot
            : defaultRoot;

        var subfolder = modelType switch
        {
            ModelType.Checkpoint => "Checkpoints",
            ModelType.LoRA => "LoRA",
            ModelType.VAE => "VAE",
            ModelType.Embedding => "Embeddings",
            ModelType.ControlNet => "ControlNet",
            _ => "Other"
        };

        return Path.Combine(root, subfolder);
    }
}
