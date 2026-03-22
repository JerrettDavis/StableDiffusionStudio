namespace StableDiffusionStudio.Domain.Enums;

/// <summary>
/// Defines how an input image is fitted to the target generation dimensions.
/// </summary>
public enum ImageInputMode
{
    /// <summary>Scale the image so the longest edge matches the target, maintaining aspect ratio. Pads the shorter edge.</summary>
    Scale,

    /// <summary>Center-crop the image to exactly match the target dimensions.</summary>
    CenterCrop,

    /// <summary>Stretch/resize the image to exactly match the target dimensions without preserving aspect ratio.</summary>
    Resize
}
