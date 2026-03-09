using FluentValidation;

namespace Chronith.Application.Commands.CustomerAuth.Refresh;

public sealed class CustomerRefreshCommandValidator : AbstractValidator<CustomerRefreshCommand>
{
    public CustomerRefreshCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
