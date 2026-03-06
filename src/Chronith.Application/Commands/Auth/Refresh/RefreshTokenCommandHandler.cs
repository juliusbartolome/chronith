using System.Security.Cryptography;
using System.Text;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.Auth.Refresh;

public sealed class RefreshTokenCommandHandler(
    ITenantUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RefreshTokenCommand, AuthTokenDto>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

    public async Task<AuthTokenDto> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken));
        var hash = Convert.ToHexStringLower(hashBytes);

        var stored = await refreshTokenRepository.GetByHashAsync(hash, cancellationToken)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        if (!stored.IsValid())
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = await userRepository.GetByIdAsync(stored.TenantUserId, cancellationToken)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        // Rotate: mark old token used, issue new one
        stored.MarkUsed();
        refreshTokenRepository.Update(stored);

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var newRefreshToken = TenantUserRefreshToken.Create(user.Id, tokenHash, RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user);
        return new AuthTokenDto(accessToken, rawToken);
    }
}
