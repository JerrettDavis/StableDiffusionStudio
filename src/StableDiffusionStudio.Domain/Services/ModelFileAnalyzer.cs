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
