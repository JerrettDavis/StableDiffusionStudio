using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Services;

public static class ModelFileAnalyzer
{
    private static readonly Dictionary<string, ModelFormat> FormatMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".safetensors"] = ModelFormat.SafeTensors,
        [".ckpt"] = ModelFormat.CKPT,
        [".gguf"] = ModelFormat.GGUF,
    };

    public static ModelFormat InferFormat(ModelFileInfo info)
    {
        var ext = Path.GetExtension(info.FileName);
        return FormatMap.GetValueOrDefault(ext, ModelFormat.Unknown);
    }

    public static ModelType InferModelType(ModelFileInfo info)
    {
        var name = info.FileName.ToLowerInvariant();

        // Filename-based (highest priority)
        if (name.Contains("controlnet") || name.Contains("control_"))
            return ModelType.ControlNet;
        if (name.Contains("upscale") || name.Contains("esrgan") || name.Contains("swinir") || name.Contains("ultrasharp"))
            return ModelType.Upscaler;
        if (name.Contains("vae") || name.Contains(".vae."))
            return ModelType.VAE;
        if (name.Contains("lora") || info.FileName.Contains("Lora/") || info.FileName.Contains("Lora\\"))
            return ModelType.LoRA;
        if (name.Contains("embedding") || name.Contains("ti-"))
            return ModelType.Embedding;

        // Size-based fallback
        var sizeMb = info.FileSize / 1_000_000.0;
        var ext = Path.GetExtension(info.FileName).ToLowerInvariant();

        return sizeMb switch
        {
            < 50 when ext is ".safetensors" or ".pt" or ".bin" => ModelType.Embedding,
            < 200 when ext is ".safetensors" => ModelType.LoRA,
            >= 300 and <= 800 when ext is ".safetensors" => ModelType.VAE,
            > 1000 => ModelType.Checkpoint,
            _ => ModelType.Unknown
        };
    }

    public static ModelFamily InferFamily(ModelFileInfo info)
    {
        if (!string.IsNullOrEmpty(info.HeaderHint))
        {
            if (info.HeaderHint.Contains("conditioner.embedders.1.model.transformer", StringComparison.OrdinalIgnoreCase))
                return ModelFamily.SDXL;
            if (info.HeaderHint.Contains("double_blocks", StringComparison.OrdinalIgnoreCase))
                return ModelFamily.Flux;
        }

        var sizeGb = info.FileSize / 1_000_000_000.0;
        return sizeGb switch
        {
            >= 10.0 => ModelFamily.Flux,
            >= 5.5 => ModelFamily.SDXL,
            >= 1.5 => ModelFamily.SD15,
            _ => ModelFamily.Unknown
        };
    }
}
