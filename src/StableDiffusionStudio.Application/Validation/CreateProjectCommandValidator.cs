using FluentValidation;
using StableDiffusionStudio.Application.Commands;

namespace StableDiffusionStudio.Application.Validation;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name must be 200 characters or fewer.");
        RuleFor(x => x.Description).MaximumLength(2000).WithMessage("Description must be 2000 characters or fewer.");
    }
}
