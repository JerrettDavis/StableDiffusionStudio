using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;
using StableDiffusionStudio.Domain.Services;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.Services;

public class SweepExpanderTests
{
    // ── helpers ──────────────────────────────────────────────────────────────────

    private static GenerationParameters BaseParams => new()
    {
        PositivePrompt = "test prompt",
        CheckpointModelId = Guid.NewGuid(),
        Steps = 20,
        CfgScale = 7.0,
        Width = 512,
        Height = 512
    };

    // ── single axis ───────────────────────────────────────────────────────────────

    [Fact]
    public void SingleAxis_ProducesCorrectCount()
    {
        var axis = SweepAxis.Categorical("Steps", ["10", "20", "30"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results.Should().HaveCount(3);
    }

    [Fact]
    public void SingleAxis_GridPositions_AreColumns_RowIsAlwaysZero()
    {
        var axis = SweepAxis.Categorical("Steps", ["10", "20", "30"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results.Select(r => r.GridX).Should().BeEquivalentTo([0, 1, 2], opts => opts.WithStrictOrdering());
        results.Select(r => r.GridY).Should().AllBeEquivalentTo(0);
    }

    // ── two axes ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TwoAxes_ProduceCartesianProduct()
    {
        var axis1 = SweepAxis.Categorical("Steps", ["10", "20", "30"]);
        var axis2 = SweepAxis.Categorical("CfgScale", ["5", "7"]);

        var results = SweepExpander.Expand(BaseParams, axis1, axis2);

        results.Should().HaveCount(6); // 3 × 2
    }

    [Fact]
    public void TwoAxes_GridPositions_CorrectXAndY()
    {
        var axis1 = SweepAxis.Categorical("Steps", ["10", "20", "30"]);
        var axis2 = SweepAxis.Categorical("CfgScale", ["5", "7"]);

        var results = SweepExpander.Expand(BaseParams, axis1, axis2);

        // Y=0 → CfgScale=5
        results.Where(r => r.GridY == 0).Select(r => r.GridX)
               .Should().BeEquivalentTo([0, 1, 2], opts => opts.WithStrictOrdering());

        // Y=1 → CfgScale=7
        results.Where(r => r.GridY == 1).Select(r => r.GridX)
               .Should().BeEquivalentTo([0, 1, 2], opts => opts.WithStrictOrdering());
    }

    // ── type-aware overrides ──────────────────────────────────────────────────────

    [Fact]
    public void CfgScaleOverride_Works_Double()
    {
        var axis = SweepAxis.Categorical("CfgScale", ["3.5", "9.0"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results[0].Parameters.CfgScale.Should().BeApproximately(3.5, precision: 1e-9);
        results[1].Parameters.CfgScale.Should().BeApproximately(9.0, precision: 1e-9);
    }

    [Fact]
    public void StepsOverride_Works_Int()
    {
        var axis = SweepAxis.Categorical("Steps", ["15", "50"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results[0].Parameters.Steps.Should().Be(15);
        results[1].Parameters.Steps.Should().Be(50);
    }

    [Fact]
    public void SamplerOverride_Works_Enum()
    {
        var axis = SweepAxis.Categorical("Sampler", ["Euler", "DDIM"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results[0].Parameters.Sampler.Should().Be(Sampler.Euler);
        results[1].Parameters.Sampler.Should().Be(Sampler.DDIM);
    }

    [Fact]
    public void PromptOverride_Works_String()
    {
        var axis = SweepAxis.Categorical("PositivePrompt", ["a cat", "a dog"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results[0].Parameters.PositivePrompt.Should().Be("a cat");
        results[1].Parameters.PositivePrompt.Should().Be("a dog");
    }

    [Fact]
    public void DenoisingStrengthOverride_Works_Double()
    {
        var axis = SweepAxis.Categorical("DenoisingStrength", ["0.4", "0.75"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results[0].Parameters.DenoisingStrength.Should().BeApproximately(0.4, precision: 1e-9);
        results[1].Parameters.DenoisingStrength.Should().BeApproximately(0.75, precision: 1e-9);
    }

    // ── non-swept parameters preserved ───────────────────────────────────────────

    [Fact]
    public void NonSweptParameters_ArePreserved()
    {
        var baseParams = BaseParams;
        var axis = SweepAxis.Categorical("Steps", ["10"]);

        var results = SweepExpander.Expand(baseParams, axis);

        results[0].Parameters.PositivePrompt.Should().Be(baseParams.PositivePrompt);
        results[0].Parameters.CfgScale.Should().Be(baseParams.CfgScale);
        results[0].Parameters.Width.Should().Be(baseParams.Width);
        results[0].Parameters.Height.Should().Be(baseParams.Height);
        results[0].Parameters.CheckpointModelId.Should().Be(baseParams.CheckpointModelId);
    }

    // ── axis values dictionary ────────────────────────────────────────────────────

    [Fact]
    public void AxisValues_TrackedCorrectly_SingleAxis()
    {
        var axis = SweepAxis.Categorical("Steps", ["10", "30"]);
        var results = SweepExpander.Expand(BaseParams, axis);

        results[0].AxisValues.Should().ContainKey("Steps").WhoseValue.Should().Be("10");
        results[1].AxisValues.Should().ContainKey("Steps").WhoseValue.Should().Be("30");
    }

    [Fact]
    public void AxisValues_TrackedCorrectly_TwoAxes()
    {
        var axis1 = SweepAxis.Categorical("Steps", ["10", "20"]);
        var axis2 = SweepAxis.Categorical("CfgScale", ["5", "9"]);

        var results = SweepExpander.Expand(BaseParams, axis1, axis2);

        foreach (var combo in results)
        {
            combo.AxisValues.Should().ContainKey("Steps");
            combo.AxisValues.Should().ContainKey("CfgScale");
        }
    }

    // ── null axis ─────────────────────────────────────────────────────────────────

    [Fact]
    public void NullAxis_ReturnsSingleCombinationWithOriginalParams()
    {
        var baseParams = BaseParams;
        var results = SweepExpander.Expand(baseParams, axis1: null);

        results.Should().HaveCount(1);
        results[0].Parameters.Should().Be(baseParams);
        results[0].GridX.Should().Be(0);
        results[0].GridY.Should().Be(0);
        results[0].AxisValues.Should().BeEmpty();
    }

    // ── unknown parameter throws ──────────────────────────────────────────────────

    [Fact]
    public void UnknownParameterName_ThrowsArgumentException()
    {
        var act = () => SweepExpander.ApplyOverride(BaseParams, "NotARealField", "42");

        act.Should().Throw<ArgumentException>().WithMessage("*NotARealField*");
    }
}
