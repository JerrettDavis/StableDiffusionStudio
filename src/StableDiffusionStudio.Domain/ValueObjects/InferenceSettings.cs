namespace StableDiffusionStudio.Domain.ValueObjects;

public sealed record InferenceSettings
{
    // Thread/CPU settings
    public int ThreadCount { get; init; } = 0; // 0 = auto (Environment.ProcessorCount)

    // Memory management
    public bool FlashAttention { get; init; } = true;
    public bool DiffusionFlashAttention { get; init; } = true;
    public bool VaeTiling { get; init; } = true;
    public bool VaeDecodeOnly { get; init; } = false; // Must be false to support img2img/inpainting
    public bool KeepClipOnCPU { get; init; } = true;
    public bool KeepVaeOnCPU { get; init; } = true;
    public bool KeepControlNetOnCPU { get; init; } = false;
    public bool OffloadParamsToCPU { get; init; } = false;

    // Performance
    public bool EnableMmap { get; init; } = false;

    public int EffectiveThreadCount => ThreadCount > 0 ? ThreadCount : Environment.ProcessorCount;

    public static InferenceSettings Default => new();

    public static InferenceSettings LowVRAM => new()
    {
        KeepClipOnCPU = true,
        KeepVaeOnCPU = true,
        KeepControlNetOnCPU = true,
        OffloadParamsToCPU = true,
        VaeTiling = true,
        DiffusionFlashAttention = true
    };

    public static InferenceSettings HighVRAM => new()
    {
        KeepClipOnCPU = false,
        KeepVaeOnCPU = false,
        KeepControlNetOnCPU = false,
        OffloadParamsToCPU = false,
        VaeTiling = false,
        DiffusionFlashAttention = true
    };
}
