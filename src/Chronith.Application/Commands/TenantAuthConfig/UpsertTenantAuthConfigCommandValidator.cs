using FluentValidation;

namespace Chronith.Application.Commands.TenantAuthConfig;

public sealed class UpsertTenantAuthConfigCommandValidator : AbstractValidator<UpsertTenantAuthConfigCommand>
{
    public UpsertTenantAuthConfigCommandValidator()
    {
        // If OIDC issuer is provided, client ID must also be provided
        RuleFor(x => x.OidcClientId)
            .NotEmpty()
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.OidcIssuer))
            .WithMessage("OIDC Client ID is required when OIDC Issuer is set.");

        RuleFor(x => x.OidcIssuer)
            .NotEmpty()
            .MaximumLength(2048)
            .When(x => !string.IsNullOrWhiteSpace(x.OidcClientId))
            .WithMessage("OIDC Issuer is required when OIDC Client ID is set.");
    }
}
