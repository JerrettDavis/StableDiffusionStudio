using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class ModelFileAnalyzerModelTypeTests
{
    [Theory]
    [InlineData("sd_xl_controlnet_canny.safetensors", ModelType.ControlNet)]
    [InlineData("control_v11p_sd15_openpose.safetensors", ModelType.ControlNet)]
    [InlineData("4x_ultrasharp.safetensors", ModelType.Upscaler)]
    [InlineData("RealESRGAN_x4plus.safetensors", ModelType.Upscaler)]
    [InlineData("4x_NMKD-Superscale-SP_178000_G_upscale.safetensors", ModelType.Upscaler)]
    [InlineData("swinir_real_sr_large_x4.safetensors", ModelType.Upscaler)]
    [InlineData("sdxl_vae.safetensors", ModelType.VAE)]
    [InlineData("model.vae.safetensors", ModelType.VAE)]
    [InlineData("my_lora_v1.safetensors", ModelType.LoRA)]
    [InlineData("character_embedding.safetensors", ModelType.Embedding)]
    [InlineData("ti-style-cyberpunk.safetensors", ModelType.Embedding)]
    public void InferModelType_FromFilename_ReturnsCorrectType(string fileName, ModelType expected)
    {
        var info = new ModelFileInfo(fileName, 100_000_000, null);

        ModelFileAnalyzer.InferModelType(info).Should().Be(expected);
    }

    [Theory]
    [InlineData("generic-model.safetensors", 10_000_000L, ModelType.Embedding)]
    [InlineData("generic-model.pt", 5_000_000L, ModelType.Embedding)]
    [InlineData("generic-model.bin", 20_000_000L, ModelType.Embedding)]
    [InlineData("generic-model.safetensors", 150_000_000L, ModelType.LoRA)]
    [InlineData("generic-model.safetensors", 500_000_000L, ModelType.VAE)]
    [InlineData("generic-model.safetensors", 2_000_000_000L, ModelType.Checkpoint)]
    public void InferModelType_FromSize_ReturnsCorrectType(string fileName, long fileSize, ModelType expected)
    {
        var info = new ModelFileInfo(fileName, fileSize, null);

        ModelFileAnalyzer.InferModelType(info).Should().Be(expected);
    }

    [Fact]
    public void InferModelType_UnknownSmallFile_ReturnsUnknown()
    {
        var info = new ModelFileInfo("generic-model.safetensors", 250_000_000L, null);

        ModelFileAnalyzer.InferModelType(info).Should().Be(ModelType.Unknown);
    }

    [Fact]
    public void InferModelType_FilenameHintTakesPriorityOverSize()
    {
        // A large file with "lora" in the name should be LoRA, not Checkpoint
        var info = new ModelFileInfo("my_lora_model.safetensors", 5_000_000_000L, null);

        ModelFileAnalyzer.InferModelType(info).Should().Be(ModelType.LoRA);
    }
}
