using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Auth.Register;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record RegisterTenantCommand(
    string TenantName,
    string TenantSlug,
    string TimeZoneId,
    string Email,
    string Password) : IRequest<AuthTokenDto>, IAuditable
{
    public Guid EntityId => Guid.Empty;
    public string EntityType => "Tenant";
    public string Action => "Create";
}

// ── Validator ────────────────────────────────────────────────────────────────

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

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class RegisterTenantCommandHandler(
    ITenantRepository tenantRepository,
    ITenantUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IDefaultTemplateSeeder templateSeeder,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterTenantCommand, AuthTokenDto>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

    public async Task<AuthTokenDto> Handle(
        RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        // Check slug uniqueness
        var slugExists = await tenantRepository.ExistsBySlugAsync(request.TenantSlug, cancellationToken);
        if (slugExists)
            throw new ConflictException($"Tenant slug '{request.TenantSlug}' is already taken.");

        // Create tenant
        var tenant = Tenant.Create(request.TenantSlug, request.TenantName, request.TimeZoneId);
        await tenantRepository.AddAsync(tenant, cancellationToken);

        // Hash password and create owner user
        var passwordHash = passwordHasher.Hash(request.Password);
        var user = TenantUser.Create(tenant.Id, request.Email, passwordHash, TenantUserRole.Owner);
        await userRepository.AddAsync(user, cancellationToken);

        // Issue refresh token
        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = TenantUserRefreshToken.Create(user.Id, tokenHash, RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Seed default notification templates for this tenant
        await templateSeeder.SeedAllAsync(tenant.Id, cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);
        return new AuthTokenDto(accessToken, rawToken);
    }
}
