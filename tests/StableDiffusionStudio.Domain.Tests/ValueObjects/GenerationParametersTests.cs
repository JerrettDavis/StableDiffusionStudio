using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class GenerationParametersTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var parameters = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = Guid.NewGuid()
        };

        parameters.NegativePrompt.Should().BeEmpty();
        parameters.VaeModelId.Should().BeNull();
        parameters.Loras.Should().BeEmpty();
        parameters.Sampler.Should().Be(Sampler.EulerA);
        parameters.Scheduler.Should().Be(Scheduler.Normal);
        parameters.Steps.Should().Be(20);
        parameters.CfgScale.Should().Be(7.0);
        parameters.Seed.Should().Be(-1);
        parameters.Width.Should().Be(512);
        parameters.Height.Should().Be(512);
        parameters.BatchSize.Should().Be(1);
        parameters.ClipSkip.Should().Be(1);
        parameters.BatchCount.Should().Be(1);
    }

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        var modelId = Guid.NewGuid();
        var a = new GenerationParameters { PositivePrompt = "test", CheckpointModelId = modelId };
        var b = new GenerationParameters { PositivePrompt = "test", CheckpointModelId = modelId };

        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var original = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = Guid.NewGuid(),
            Steps = 20
        };

        var modified = original with { Steps = 30 };

        modified.Steps.Should().Be(30);
        original.Steps.Should().Be(20);
        modified.PositivePrompt.Should().Be(original.PositivePrompt);
    }

    [Fact]
    public void ClipSkip_CanBeSet()
    {
        var parameters = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = Guid.NewGuid(),
            ClipSkip = 2
        };

        parameters.ClipSkip.Should().Be(2);
    }

    [Fact]
    public void BatchCount_CanBeSet()
    {
        var parameters = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = Guid.NewGuid(),
            BatchCount = 3
        };

        parameters.BatchCount.Should().Be(3);
    }

    [Fact]
    public void LoraReference_DefaultWeight_IsOne()
    {
        var lora = new LoraReference(Guid.NewGuid());
        lora.Weight.Should().Be(1.0);
    }

    [Fact]
    public void LoraReference_CustomWeight_IsSet()
    {
        var lora = new LoraReference(Guid.NewGuid(), 0.7);
        lora.Weight.Should().Be(0.7);
    }

    [Fact]
    public void LoraReference_Equality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = new LoraReference(id, 0.8);
        var b = new LoraReference(id, 0.8);
        a.Should().Be(b);
    }

    [Fact]
    public void LoraReference_Inequality_DifferentWeights()
    {
        var id = Guid.NewGuid();
        var a = new LoraReference(id, 0.5);
        var b = new LoraReference(id, 0.8);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Parameters_WithLoras_CanBeCreated()
    {
        var loraId = Guid.NewGuid();
        var parameters = new GenerationParameters
        {
            PositivePrompt = "test",
            CheckpointModelId = Guid.NewGuid(),
            Loras = [new LoraReference(loraId, 0.8), new LoraReference(Guid.NewGuid(), 1.2)]
        };

        parameters.Loras.Should().HaveCount(2);
        parameters.Loras[0].ModelId.Should().Be(loraId);
        parameters.Loras[0].Weight.Should().Be(0.8);
    }

    [Fact]
    public void Parameters_AllProperties_CanBeSetViaInit()
    {
        var checkpointId = Guid.NewGuid();
        var vaeId = Guid.NewGuid();
        var parameters = new GenerationParameters
        {
            PositivePrompt = "custom prompt",
            NegativePrompt = "custom negative",
            CheckpointModelId = checkpointId,
            VaeModelId = vaeId,
            Sampler = Sampler.DPMPlusPlus2MKarras,
            Scheduler = Scheduler.Exponential,
            Steps = 50,
            CfgScale = 12.5,
            Seed = 42,
            Width = 1024,
            Height = 768,
            BatchSize = 4,
            ClipSkip = 2,
            BatchCount = 3
        };

        parameters.PositivePrompt.Should().Be("custom prompt");
        parameters.NegativePrompt.Should().Be("custom negative");
        parameters.CheckpointModelId.Should().Be(checkpointId);
        parameters.VaeModelId.Should().Be(vaeId);
        parameters.Sampler.Should().Be(Sampler.DPMPlusPlus2MKarras);
        parameters.Scheduler.Should().Be(Scheduler.Exponential);
        parameters.Steps.Should().Be(50);
        parameters.CfgScale.Should().Be(12.5);
        parameters.Seed.Should().Be(42);
        parameters.Width.Should().Be(1024);
        parameters.Height.Should().Be(768);
        parameters.BatchSize.Should().Be(4);
        parameters.ClipSkip.Should().Be(2);
        parameters.BatchCount.Should().Be(3);
    }
}
