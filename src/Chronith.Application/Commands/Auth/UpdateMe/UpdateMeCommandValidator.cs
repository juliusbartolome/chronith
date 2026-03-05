using FluentValidation;

namespace Chronith.Application.Commands.Auth.UpdateMe;

public sealed class UpdateMeCommandValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeCommandValidator()
    {
        When(x => x.Email is not null, () =>
            RuleFor(x => x.Email!).EmailAddress());
        When(x => x.NewPassword is not null, () =>
            RuleFor(x => x.NewPassword!).MinimumLength(8)
                .Matches("[0-9]").WithMessage("Password must contain at least one digit."));
    }
}
