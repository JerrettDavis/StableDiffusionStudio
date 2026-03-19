using FluentValidation;
using StableDiffusionStudio.Application.Commands;

namespace StableDiffusionStudio.Application.Validation;

public class RenameProjectCommandValidator : AbstractValidator<RenameProjectCommand>
{
    public RenameProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewName).NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name must be 200 characters or fewer.");
    }
}
