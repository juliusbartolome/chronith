using FluentValidation;

namespace Chronith.Application.Commands.CustomerAuth.OidcLogin;

public sealed class CustomerOidcLoginCommandValidator : AbstractValidator<CustomerOidcLoginCommand>
{
    public CustomerOidcLoginCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.IdToken).NotEmpty();
    }
}
