using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Auth.Login;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record LoginCommand(
    string TenantSlug,
    string Email,
    string Password) : IRequest<AuthTokenDto>;

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty();
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class LoginCommandHandler(
    ITenantRepository tenantRepository,
    ITenantUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LoginCommand, AuthTokenDto>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

    public async Task<AuthTokenDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Intentionally vague error message to avoid email enumeration
        const string invalidCredentials = "Invalid credentials.";

        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken)
            ?? throw new UnauthorizedException(invalidCredentials);

        var user = await userRepository.GetByEmailAsync(tenant.Id, request.Email, cancellationToken)
            ?? throw new UnauthorizedException(invalidCredentials);

        if (!user.IsActive)
            throw new UnauthorizedException(invalidCredentials);

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException(invalidCredentials);

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = TenantUserRefreshToken.Create(user.Id, tokenHash, RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);
        return new AuthTokenDto(accessToken, rawToken);
    }
}
