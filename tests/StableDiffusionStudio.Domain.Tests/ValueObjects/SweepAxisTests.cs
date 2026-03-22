using FluentAssertions;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.ValueObjects;

public class SweepAxisTests
{
    [Fact]
    public void Numeric_GeneratesCorrectValues()
    {
        var axis = SweepAxis.Numeric("CfgScale", 1.0, 3.0, 0.5);
        axis.Values.Should().BeEquivalentTo(["1", "1.5", "2", "2.5", "3"]);
        axis.ParameterName.Should().Be("CfgScale");
    }

    [Fact]
    public void Numeric_IntegerSteps_GeneratesWholeNumbers()
    {
        var axis = SweepAxis.Numeric("Steps", 5, 20, 5);
        axis.Values.Should().BeEquivalentTo(["5", "10", "15", "20"]);
    }

    [Fact]
    public void Numeric_SingleValue_WhenStartEqualsEnd()
    {
        var axis = SweepAxis.Numeric("CfgScale", 5.0, 5.0, 1.0);
        axis.Values.Should().HaveCount(1);
        axis.Values[0].Should().Be("5");
    }

    [Fact]
    public void Numeric_ZeroStep_Throws()
    {
        var act = () => SweepAxis.Numeric("CfgScale", 1.0, 5.0, 0);
        act.Should().Throw<ArgumentException>().WithMessage("*Step*positive*");
    }

    [Fact]
    public void Numeric_StartGreaterThanEnd_Throws()
    {
        var act = () => SweepAxis.Numeric("CfgScale", 10.0, 1.0, 1.0);
        act.Should().Throw<ArgumentException>().WithMessage("*Start*");
    }

    [Fact]
    public void Categorical_CreatesFromValues()
    {
        var axis = SweepAxis.Categorical("Sampler", ["EulerA", "DDIM", "DPMPlusPlus2M"]);
        axis.Values.Should().HaveCount(3);
        axis.ParameterName.Should().Be("Sampler");
    }

    [Fact]
    public void Categorical_EmptyValues_Throws()
    {
        var act = () => SweepAxis.Categorical("Sampler", []);
        act.Should().Throw<ArgumentException>().WithMessage("*At least one*");
    }

    [Fact]
    public void DisplayLabel_FallsBackToParameterName()
    {
        var axis = SweepAxis.Categorical("Sampler", ["EulerA"]);
        axis.DisplayLabel.Should().Be("Sampler");

        var labeled = SweepAxis.Categorical("Sampler", ["EulerA"], label: "Sampling Method");
        labeled.DisplayLabel.Should().Be("Sampling Method");
    }
}
