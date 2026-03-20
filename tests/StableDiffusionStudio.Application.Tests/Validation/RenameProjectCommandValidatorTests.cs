using FluentAssertions;
using StableDiffusionStudio.Application.Commands;
using StableDiffusionStudio.Application.Validation;

namespace StableDiffusionStudio.Application.Tests.Validation;

public class RenameProjectCommandValidatorTests
{
    private readonly RenameProjectCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_IsValid()
    {
        var result = _validator.Validate(new RenameProjectCommand(Guid.NewGuid(), "New Name"));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_IsInvalid()
    {
        var result = _validator.Validate(new RenameProjectCommand(Guid.Empty, "New Name"));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void Validate_EmptyName_IsInvalid()
    {
        var result = _validator.Validate(new RenameProjectCommand(Guid.NewGuid(), ""));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewName");
    }

    [Fact]
    public void Validate_NameTooLong_IsInvalid()
    {
        var result = _validator.Validate(new RenameProjectCommand(Guid.NewGuid(), new string('A', 201)));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewName");
    }

    [Fact]
    public void Validate_NameAtMaxLength_IsValid()
    {
        var result = _validator.Validate(new RenameProjectCommand(Guid.NewGuid(), new string('A', 200)));
        result.IsValid.Should().BeTrue();
    }
}
