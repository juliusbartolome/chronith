using Chronith.Application.Constants;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.OidcLogin;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CustomerOidcLoginCommand(
    string TenantSlug,
    string IdToken) : IRequest<CustomerAuthTokenDto>;

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class CustomerOidcLoginCommandValidator : AbstractValidator<CustomerOidcLoginCommand>
{
    public CustomerOidcLoginCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.IdToken).NotEmpty();
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class CustomerOidcLoginCommandHandler(
    ITenantRepository tenantRepository,
    ITenantAuthConfigRepository authConfigRepository,
    ICustomerRepository customerRepository,
    ICustomerRefreshTokenRepository refreshTokenRepository,
    IOidcTokenValidator oidcTokenValidator,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CustomerOidcLoginCommand, CustomerAuthTokenDto>
{

    public async Task<CustomerAuthTokenDto> Handle(
        CustomerOidcLoginCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantSlug);

        var authConfig = await authConfigRepository.GetByTenantIdAsync(tenant.Id, cancellationToken)
            ?? throw new UnauthorizedException("OIDC is not configured for this tenant.");

        if (string.IsNullOrWhiteSpace(authConfig.OidcIssuer) ||
            string.IsNullOrWhiteSpace(authConfig.OidcClientId))
            throw new UnauthorizedException("OIDC is not configured for this tenant.");

        var validation = await oidcTokenValidator.ValidateAsync(
            request.IdToken, authConfig.OidcIssuer, authConfig.OidcClientId,
            authConfig.OidcAudience, cancellationToken);

        if (!validation.IsValid)
            throw new UnauthorizedException(validation.Error ?? "Invalid OIDC token.");

        // Try to find existing customer by external ID
        var customer = await customerRepository.GetByExternalIdAsync(
            tenant.Id, validation.ExternalId!, cancellationToken);

        if (customer is not null)
        {
            customer.RecordLogin();
            customerRepository.Update(customer);
        }
        else
        {
            // Auto-create customer from OIDC claims
            customer = Customer.CreateOidc(
                tenant.Id,
                validation.Email ?? $"{validation.ExternalId}@oidc.unknown",
                validation.Name ?? "OIDC User",
                validation.ExternalId!,
                authConfig.OidcIssuer);

            await customerRepository.AddAsync(customer, cancellationToken);
        }

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = CustomerRefreshToken.Create(customer.Id, tokenHash, CustomerAuthConstants.RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateCustomerAccessToken(customer);
        return new CustomerAuthTokenDto(accessToken, rawToken, customer.ToDto());
    }
}
