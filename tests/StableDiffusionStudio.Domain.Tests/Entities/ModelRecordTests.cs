using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ModelRecordTests
{
    [Fact]
    public void Create_WithValidInputs_SetsProperties()
    {
        var record = ModelRecord.Create("Stable Diffusion v1.5", "/models/sd-v1-5.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 4_000_000_000L, "local-folder");

        record.Id.Should().NotBeEmpty();
        record.Title.Should().Be("Stable Diffusion v1.5");
        record.FilePath.Should().Be("/models/sd-v1-5.safetensors");
        record.ModelFamily.Should().Be(ModelFamily.SD15);
        record.Format.Should().Be(ModelFormat.SafeTensors);
        record.FileSize.Should().Be(4_000_000_000L);
        record.Source.Should().Be("local-folder");
        record.Status.Should().Be(ModelStatus.Available);
    }

    [Fact]
    public void Create_WithoutTitle_DefaultsToFilename()
    {
        var record = ModelRecord.Create(null, "/models/my-cool-model.safetensors",
            ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "local");

        record.Title.Should().Be("my-cool-model.safetensors");
    }

    [Fact]
    public void Create_WithEmptyFilePath_ThrowsArgumentException()
    {
        var act = () => ModelRecord.Create(null, "", ModelFamily.Unknown, ModelFormat.Unknown, 0, "local");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkMissing_SetsStatusToMissing()
    {
        var record = ModelRecord.Create(null, "/path/model.ckpt", ModelFamily.Unknown, ModelFormat.CKPT, 1000, "local");

        record.MarkMissing();

        record.Status.Should().Be(ModelStatus.Missing);
    }

    [Fact]
    public void MarkAvailable_SetsStatusToAvailable()
    {
        var record = ModelRecord.Create(null, "/path/model.ckpt", ModelFamily.Unknown, ModelFormat.CKPT, 1000, "local");
        record.MarkMissing();

        record.MarkAvailable();

        record.Status.Should().Be(ModelStatus.Available);
    }

    [Fact]
    public void Create_WithModelType_SetsTypeCorrectly()
    {
        var record = ModelRecord.Create("LoRA Model", "/models/lora.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 100_000_000L, "local-folder", ModelType.LoRA);

        record.Type.Should().Be(ModelType.LoRA);
    }

    [Fact]
    public void Create_WithoutModelType_DefaultsToCheckpoint()
    {
        var record = ModelRecord.Create("Model", "/models/model.safetensors",
            ModelFamily.SD15, ModelFormat.SafeTensors, 2_000_000_000L, "local-folder");

        record.Type.Should().Be(ModelType.Checkpoint);
    }

    [Fact]
    public void UpdateMetadata_WithType_UpdatesType()
    {
        var record = ModelRecord.Create(null, "/path/model.safetensors", ModelFamily.Unknown,
            ModelFormat.SafeTensors, 1000, "local");

        record.UpdateMetadata(type: ModelType.VAE);

        record.Type.Should().Be(ModelType.VAE);
    }

    [Fact]
    public void UpdateMetadata_UpdatesFields()
    {
        var record = ModelRecord.Create(null, "/path/model.safetensors", ModelFamily.Unknown, ModelFormat.SafeTensors, 1000, "local");

        record.UpdateMetadata(
            title: "Updated Title",
            modelFamily: ModelFamily.SDXL,
            description: "A fine model",
            tags: new[] { "landscape", "photorealistic" },
            previewImagePath: "/path/preview.png",
            compatibilityHints: "Requires 8GB VRAM");

        record.Title.Should().Be("Updated Title");
        record.ModelFamily.Should().Be(ModelFamily.SDXL);
        record.Description.Should().Be("A fine model");
        record.Tags.Should().BeEquivalentTo(new[] { "landscape", "photorealistic" });
        record.PreviewImagePath.Should().Be("/path/preview.png");
        record.CompatibilityHints.Should().Be("Requires 8GB VRAM");
    }
}
