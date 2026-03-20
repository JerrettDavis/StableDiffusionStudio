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
}
