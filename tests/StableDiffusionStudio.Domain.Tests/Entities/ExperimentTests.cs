using FluentAssertions;
using StableDiffusionStudio.Domain.Entities;
using StableDiffusionStudio.Domain.ValueObjects;

namespace StableDiffusionStudio.Domain.Tests.Entities;

public class ExperimentTests
{
    private static GenerationParameters ValidParameters => new()
    {
        PositivePrompt = "a beautiful landscape",
        CheckpointModelId = Guid.NewGuid()
    };

    private static IReadOnlyList<SweepAxis> OneAxis =>
        [SweepAxis.Numeric("cfg_scale", 5, 10, 1)];

    private static IReadOnlyList<SweepAxis> TwoAxes =>
        [SweepAxis.Numeric("cfg_scale", 5, 10, 1), SweepAxis.Categorical("sampler", ["euler", "dpm"])];

    private static IReadOnlyList<SweepAxis> ThreeAxes =>
        [SweepAxis.Numeric("cfg_scale", 5, 10, 1), SweepAxis.Categorical("sampler", ["euler"]), SweepAxis.Numeric("steps", 10, 30, 10)];

    [Fact]
    public void Create_WithValidInputs_SetsPropertiesCorrectly()
    {
        var experiment = Experiment.Create("My Experiment", ValidParameters, OneAxis);

        experiment.Id.Should().NotBeEmpty();
        experiment.Name.Should().Be("My Experiment");
        experiment.Description.Should().BeNull();
        experiment.InitImagePath.Should().BeNull();
        experiment.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        experiment.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        experiment.Runs.Should().BeEmpty();
        experiment.BaseParametersJson.Should().NotBeNullOrWhiteSpace();
        experiment.SweepAxesJson.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var act = () => Experiment.Create(name!, ValidParameters, OneAxis);
        act.Should().Throw<ArgumentException>().WithMessage("*name*");
    }

    [Fact]
    public void Create_WithNoAxes_ThrowsArgumentException()
    {
        var act = () => Experiment.Create("Test", ValidParameters, []);
        act.Should().Throw<ArgumentException>().WithMessage("*axis*");
    }

    [Fact]
    public void Create_WithThreeAxes_ThrowsArgumentException()
    {
        var act = () => Experiment.Create("Test", ValidParameters, ThreeAxes);
        act.Should().Throw<ArgumentException>().WithMessage("*two*");
    }

    [Fact]
    public void Create_WithTwoAxes_Succeeds()
    {
        var experiment = Experiment.Create("Test", ValidParameters, TwoAxes);
        experiment.GetSweepAxes().Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithInitImagePath_SetsInitImagePath()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis, "/path/to/init.png");
        experiment.InitImagePath.Should().Be("/path/to/init.png");
    }

    [Fact]
    public void CreateRun_AddsRunToCollection()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);

        var run = experiment.CreateRun(6, 42L);

        experiment.Runs.Should().HaveCount(1);
        experiment.Runs[0].Should().BeSameAs(run);
        run.ExperimentId.Should().Be(experiment.Id);
    }

    [Fact]
    public void CreateRun_MultipleRuns_AllAddedToCollection()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);

        experiment.CreateRun(6, 1L);
        experiment.CreateRun(6, 2L);

        experiment.Runs.Should().HaveCount(2);
    }

    [Fact]
    public void GetBaseParameters_RoundTripsCorrectly()
    {
        var parameters = ValidParameters;
        var experiment = Experiment.Create("Test", parameters, OneAxis);

        var result = experiment.GetBaseParameters();

        result.PositivePrompt.Should().Be(parameters.PositivePrompt);
        result.CheckpointModelId.Should().Be(parameters.CheckpointModelId);
    }

    [Fact]
    public void GetSweepAxes_RoundTripsCorrectly()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);

        var result = experiment.GetSweepAxes();

        result.Should().HaveCount(1);
        result[0].ParameterName.Should().Be("cfg_scale");
    }

    [Fact]
    public void UpdateName_WithValidName_UpdatesName()
    {
        var experiment = Experiment.Create("Old Name", ValidParameters, OneAxis);

        experiment.UpdateName("New Name");

        experiment.Name.Should().Be("New Name");
        experiment.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateName_WithEmptyName_ThrowsArgumentException(string? name)
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);
        var act = () => experiment.UpdateName(name!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDescription_SetsDescription()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);

        experiment.UpdateDescription("A description");

        experiment.Description.Should().Be("A description");
    }

    [Fact]
    public void UpdateDescription_WithNull_ClearsDescription()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);
        experiment.UpdateDescription("old desc");

        experiment.UpdateDescription(null);

        experiment.Description.Should().BeNull();
    }

    [Fact]
    public void UpdateConfiguration_UpdatesParamsAndAxes()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);
        var newParams = ValidParameters with { Steps = 30 };
        var newAxes = TwoAxes;

        experiment.UpdateConfiguration(newParams, newAxes, "/new/init.png");

        experiment.GetBaseParameters().Steps.Should().Be(30);
        experiment.GetSweepAxes().Should().HaveCount(2);
        experiment.InitImagePath.Should().Be("/new/init.png");
        experiment.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateConfiguration_WithNoAxes_ThrowsArgumentException()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);
        var act = () => experiment.UpdateConfiguration(ValidParameters, []);
        act.Should().Throw<ArgumentException>().WithMessage("*axis*");
    }

    [Fact]
    public void UpdateConfiguration_WithThreeAxes_ThrowsArgumentException()
    {
        var experiment = Experiment.Create("Test", ValidParameters, OneAxis);
        var act = () => experiment.UpdateConfiguration(ValidParameters, ThreeAxes);
        act.Should().Throw<ArgumentException>().WithMessage("*two*");
    }
}
