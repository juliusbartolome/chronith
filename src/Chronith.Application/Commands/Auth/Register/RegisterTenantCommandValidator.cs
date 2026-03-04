using FluentValidation;

namespace Chronith.Application.Commands.Auth.Register;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TenantSlug).NotEmpty().Matches("^[a-z0-9-]+$")
            .WithMessage("Slug must contain only lowercase letters, digits, and hyphens.");
        RuleFor(x => x.TimeZoneId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
