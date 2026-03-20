using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Services;

/// <summary>
/// Recommended generation parameter defaults for each model family.
/// Applied automatically when a checkpoint is selected in the workspace.
/// </summary>
public static class ModelFamilyPresets
{
    public static GenerationPreset GetPreset(ModelFamily family) => family switch
    {
        ModelFamily.SD15 => SD15,
        ModelFamily.SDXL => SDXL,
        ModelFamily.Flux => Flux,
        _ => SD15 // Safe fallback
    };

    public static GenerationPreset SD15 { get; } = new(
        Name: "SD 1.5 Defaults",
        Family: ModelFamily.SD15,
        Sampler: Sampler.EulerA,
        Scheduler: Scheduler.Normal,
        Steps: 20,
        CfgScale: 7.0,
        Width: 512,
        Height: 512,
        NegativePrompt: "ugly, blurry, low quality, deformed, disfigured");

    public static GenerationPreset SDXL { get; } = new(
        Name: "SDXL Defaults",
        Family: ModelFamily.SDXL,
        Sampler: Sampler.DPMPlusPlus2MKarras,
        Scheduler: Scheduler.Karras,
        Steps: 25,
        CfgScale: 7.0,
        Width: 1024,
        Height: 1024,
        NegativePrompt: "ugly, blurry, low quality, deformed, disfigured, watermark, text");

    public static GenerationPreset Flux { get; } = new(
        Name: "Flux Defaults",
        Family: ModelFamily.Flux,
        Sampler: Sampler.Euler,
        Scheduler: Scheduler.Normal,
        Steps: 20,
        CfgScale: 3.5,
        Width: 1024,
        Height: 1024,
        NegativePrompt: "");

    public static IReadOnlyList<GenerationPreset> All { get; } = [SD15, SDXL, Flux];
}

public sealed record GenerationPreset(
    string Name,
    ModelFamily Family,
    Sampler Sampler,
    Scheduler Scheduler,
    int Steps,
    double CfgScale,
    int Width,
    int Height,
    string NegativePrompt);
