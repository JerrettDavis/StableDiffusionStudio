using FluentAssertions;
using StableDiffusionStudio.Domain.Enums;

namespace StableDiffusionStudio.Domain.Tests.Enums;

public class ExperimentRunStatusTests
{
    [Fact]
    public void AllExpectedValues_Exist()
    {
        Enum.GetValues<ExperimentRunStatus>().Should().HaveCount(5);
        Enum.IsDefined(ExperimentRunStatus.Pending).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Running).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Completed).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Failed).Should().BeTrue();
        Enum.IsDefined(ExperimentRunStatus.Cancelled).Should().BeTrue();
    }
}
