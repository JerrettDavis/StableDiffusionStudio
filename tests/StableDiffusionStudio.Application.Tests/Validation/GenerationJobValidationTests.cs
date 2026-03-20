using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Application.Tests.Validation;

/// <summary>
/// Tests the domain validation of GenerationJob.Create through the application command path.
/// These test the validation rules that fire when creating generation jobs.
/// </summary>
public class GenerationJobValidationTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid ModelId = Guid.NewGuid();

    private static GenerationParameters ValidParams() => new()
    {
        PositivePrompt = "a cat",
        CheckpointModelId = ModelId,
        Steps = 20,
        CfgScale = 7.0,
        Width = 512,
        Height = 512,
        BatchSize = 1
    };

    [Fact]
    public void ValidParameters_CreatesJob()
    {
        var job = GenerationJob.Create(ProjectId, ValidParams());
        job.Should().NotBeNull();
        job.Status.Should().Be(GenerationJobStatus.Pending);
    }

    [Fact]
    public void EmptyPrompt_Throws()
    {
        var p = ValidParams() with { PositivePrompt = "" };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*prompt*");
    }

    [Fact]
    public void WhitespacePrompt_Throws()
    {
        var p = ValidParams() with { PositivePrompt = "   " };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Steps_LessThan1_Throws()
    {
        var p = ValidParams() with { Steps = 0 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*Steps*");
    }

    [Fact]
    public void Steps_GreaterThan150_Throws()
    {
        var p = ValidParams() with { Steps = 151 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*Steps*");
    }

    [Fact]
    public void Steps_AtBoundary1_Succeeds()
    {
        var p = ValidParams() with { Steps = 1 };
        var job = GenerationJob.Create(ProjectId, p);
        job.Parameters.Steps.Should().Be(1);
    }

    [Fact]
    public void Steps_AtBoundary150_Succeeds()
    {
        var p = ValidParams() with { Steps = 150 };
        var job = GenerationJob.Create(ProjectId, p);
        job.Parameters.Steps.Should().Be(150);
    }

    [Fact]
    public void Width_NotMultipleOf64_Throws()
    {
        var p = ValidParams() with { Width = 500 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*Width*height*64*");
    }

    [Fact]
    public void Height_NotMultipleOf64_Throws()
    {
        var p = ValidParams() with { Height = 513 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*Width*height*64*");
    }

    [Fact]
    public void Width_MultipleOf64_Succeeds()
    {
        var p = ValidParams() with { Width = 768 };
        var job = GenerationJob.Create(ProjectId, p);
        job.Parameters.Width.Should().Be(768);
    }

    [Fact]
    public void CfgScale_LessThan1_Throws()
    {
        var p = ValidParams() with { CfgScale = 0.5 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*CFG*");
    }

    [Fact]
    public void CfgScale_GreaterThan30_Throws()
    {
        var p = ValidParams() with { CfgScale = 31 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*CFG*");
    }

    [Fact]
    public void CfgScale_AtBoundary1_Succeeds()
    {
        var p = ValidParams() with { CfgScale = 1 };
        var job = GenerationJob.Create(ProjectId, p);
        job.Parameters.CfgScale.Should().Be(1);
    }

    [Fact]
    public void CfgScale_AtBoundary30_Succeeds()
    {
        var p = ValidParams() with { CfgScale = 30 };
        var job = GenerationJob.Create(ProjectId, p);
        job.Parameters.CfgScale.Should().Be(30);
    }

    [Fact]
    public void BatchSize_LessThan1_Throws()
    {
        var p = ValidParams() with { BatchSize = 0 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*Batch*");
    }

    [Fact]
    public void BatchSize_GreaterThan16_Throws()
    {
        var p = ValidParams() with { BatchSize = 17 };
        var act = () => GenerationJob.Create(ProjectId, p);
        act.Should().Throw<ArgumentException>().WithMessage("*Batch*");
    }

    [Fact]
    public void BatchSize_AtBoundary16_Succeeds()
    {
        var p = ValidParams() with { BatchSize = 16 };
        var job = GenerationJob.Create(ProjectId, p);
        job.Parameters.BatchSize.Should().Be(16);
    }
}
