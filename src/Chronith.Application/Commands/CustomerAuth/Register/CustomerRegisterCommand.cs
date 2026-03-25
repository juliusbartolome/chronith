using Chronith.Application.Behaviors;
using Chronith.Application.Constants;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Register;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CustomerRegisterCommand : IRequest<CustomerAuthTokenDto>, IAuditable, IPlanEnforcedCommand
{
    public required string TenantSlug { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Mobile { get; init; }

    public Guid EntityId => Guid.Empty;
    public string EntityType => "Customer";
    public string Action => "Create";

    public string EnforcedResourceType => "Customer";
}

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class CustomerRegisterCommandValidator : AbstractValidator<CustomerRegisterCommand>
{
    public CustomerRegisterCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"\d").WithMessage("Password must contain at least one digit.");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class CustomerRegisterCommandHandler(
    ITenantRepository tenantRepository,
    ITenantAuthConfigRepository authConfigRepository,
    ICustomerRepository customerRepository,
    ICustomerRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CustomerRegisterCommand, CustomerAuthTokenDto>
{

    public async Task<CustomerAuthTokenDto> Handle(
        CustomerRegisterCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken)
            ?? throw new NotFoundException("Tenant", request.TenantSlug);

        var authConfig = await authConfigRepository.GetByTenantIdAsync(tenant.Id, cancellationToken);
        if (authConfig is not null && !authConfig.AllowBuiltInAuth)
            throw new UnauthorizedException("Built-in authentication is disabled for this tenant.");

        if (authConfig is not null && authConfig.MagicLinkEnabled)
            throw new InvalidOperationException(
                $"Magic link is enabled for this tenant. Use POST /public/{request.TenantSlug}/auth/magic-link/register instead.");

        var existing = await customerRepository.GetByEmailAsync(tenant.Id, request.Email, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A customer with email '{request.Email}' already exists.");

        var passwordHash = passwordHasher.Hash(request.Password);
        var customer = Customer.Create(tenant.Id, request.Email, passwordHash, request.FirstName,
            request.LastName, request.Mobile, "builtin");

        await customerRepository.AddAsync(customer, cancellationToken);

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = CustomerRefreshToken.Create(customer.Id, tokenHash, CustomerAuthConstants.RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateCustomerAccessToken(customer);
        return new CustomerAuthTokenDto(accessToken, rawToken, customer.ToDto());
    }
}
