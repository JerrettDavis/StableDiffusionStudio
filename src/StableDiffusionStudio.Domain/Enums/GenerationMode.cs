namespace StableDiffusionStudio.Domain.Enums;

/// <summary>
/// Defines the generation pipeline mode.
/// </summary>
public enum GenerationMode
{
    /// <summary>Text-to-image: generate from a text prompt only.</summary>
    TextToImage,

    /// <summary>Image-to-image: generate using an init image + prompt with denoising.</summary>
    ImageToImage
}
