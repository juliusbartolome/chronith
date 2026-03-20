using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.Auth.Register;

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
