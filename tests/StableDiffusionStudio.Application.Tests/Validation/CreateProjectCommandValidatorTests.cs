using FluentAssertions;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Validation;

namespace StableDiffusionStudio.Application.Tests.Validation;

public class CreateProjectCommandValidatorTests
{
    private readonly CreateProjectCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_IsValid()
    {
        var result = _validator.Validate(new CreateProjectCommand("Test", null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_IsInvalid()
    {
        var result = _validator.Validate(new CreateProjectCommand("", null));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameTooLong_IsInvalid()
    {
        var result = _validator.Validate(new CreateProjectCommand(new string('A', 201), null));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DescriptionTooLong_IsInvalid()
    {
        var result = _validator.Validate(new CreateProjectCommand("OK", new string('A', 2001)));
        result.IsValid.Should().BeFalse();
    }
}
