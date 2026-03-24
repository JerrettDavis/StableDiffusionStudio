using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.ValueObjects;

/// <summary>
/// A set of generation defaults that can be associated with a model family
/// (string-based, e.g. "SD 1.5", "Flux.1 D", "Pony") or a specific checkpoint.
/// Settings are nullable — null means "inherit from parent" in the cascade:
/// App Defaults → Family Rule → Checkpoint Rule.
/// </summary>
public sealed record ModelRuleSet
{
    // Sampling
    public int? Steps { get; init; }
    public double? CfgScale { get; init; }
    public Sampler? Sampler { get; init; }
    public Scheduler? Scheduler { get; init; }
    public int? ClipSkip { get; init; }
    public double? Eta { get; init; }

    // Dimensions
    public int? Width { get; init; }
    public int? Height { get; init; }

    // img2img / denoising
    public double? DenoisingStrength { get; init; }
    public ImageInputMode? ImageInputMode { get; init; }

    // Hires fix
    public bool? HiresFixEnabled { get; init; }
    public double? HiresUpscaleFactor { get; init; }
    public int? HiresSteps { get; init; }
    public double? HiresDenoisingStrength { get; init; }

    // Prompts
    public string? DefaultNegativePrompt { get; init; }

    /// <summary>
    /// Merge this rule set with a parent (less specific) rule set.
    /// This rule's non-null values override the parent's values.
    /// </summary>
    public ModelRuleSet MergeOver(ModelRuleSet parent) => new()
    {
        Steps = Steps ?? parent.Steps,
        CfgScale = CfgScale ?? parent.CfgScale,
        Sampler = Sampler ?? parent.Sampler,
        Scheduler = Scheduler ?? parent.Scheduler,
        ClipSkip = ClipSkip ?? parent.ClipSkip,
        Eta = Eta ?? parent.Eta,
        Width = Width ?? parent.Width,
        Height = Height ?? parent.Height,
        DenoisingStrength = DenoisingStrength ?? parent.DenoisingStrength,
        ImageInputMode = ImageInputMode ?? parent.ImageInputMode,
        HiresFixEnabled = HiresFixEnabled ?? parent.HiresFixEnabled,
        HiresUpscaleFactor = HiresUpscaleFactor ?? parent.HiresUpscaleFactor,
        HiresSteps = HiresSteps ?? parent.HiresSteps,
        HiresDenoisingStrength = HiresDenoisingStrength ?? parent.HiresDenoisingStrength,
        DefaultNegativePrompt = DefaultNegativePrompt ?? parent.DefaultNegativePrompt,
    };

    /// <summary>App-wide defaults when no rules are configured.</summary>
    public static ModelRuleSet AppDefaults => new()
    {
        Steps = 20,
        CfgScale = 7.0,
        Sampler = Enums.Sampler.EulerA,
        Scheduler = Enums.Scheduler.Normal,
        ClipSkip = 1,
        Eta = 0.0,
        Width = 512,
        Height = 512,
        DenoisingStrength = 1.0,
        ImageInputMode = Enums.ImageInputMode.Scale,
        HiresFixEnabled = false,
        HiresUpscaleFactor = 2.0,
        HiresSteps = 0,
        HiresDenoisingStrength = 0.55,
        DefaultNegativePrompt = "",
    };

    /// <summary>Apply this resolved rule set to GenerationParameters.</summary>
    public GenerationParameters ApplyTo(GenerationParameters parameters) => parameters with
    {
        Steps = Steps ?? parameters.Steps,
        CfgScale = CfgScale ?? parameters.CfgScale,
        Sampler = Sampler ?? parameters.Sampler,
        Scheduler = Scheduler ?? parameters.Scheduler,
        ClipSkip = ClipSkip ?? parameters.ClipSkip,
        Eta = Eta ?? parameters.Eta,
        Width = Width ?? parameters.Width,
        Height = Height ?? parameters.Height,
        DenoisingStrength = DenoisingStrength ?? parameters.DenoisingStrength,
        ImageInputMode = ImageInputMode ?? parameters.ImageInputMode,
        HiresFixEnabled = HiresFixEnabled ?? parameters.HiresFixEnabled,
        HiresUpscaleFactor = HiresUpscaleFactor ?? parameters.HiresUpscaleFactor,
        HiresSteps = HiresSteps ?? parameters.HiresSteps,
        HiresDenoisingStrength = HiresDenoisingStrength ?? parameters.HiresDenoisingStrength,
        NegativePrompt = !string.IsNullOrEmpty(DefaultNegativePrompt) && string.IsNullOrEmpty(parameters.NegativePrompt)
            ? DefaultNegativePrompt
            : parameters.NegativePrompt,
    };
}
