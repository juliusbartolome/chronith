using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Constants;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Refresh;

public sealed class CustomerRefreshCommandHandler(
    ITenantRepository tenantRepository,
    ICustomerRepository customerRepository,
    ICustomerRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CustomerRefreshCommand, CustomerAuthTokenDto>
{

    public async Task<CustomerAuthTokenDto> Handle(
        CustomerRefreshCommand request, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken));
        var hash = Convert.ToHexStringLower(hashBytes);

        var stored = await refreshTokenRepository.GetByHashAsync(hash, cancellationToken)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        if (!stored.IsValid())
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var customer = await customerRepository.GetByIdCrossTenantAsync(stored.CustomerId, cancellationToken)
            ?? throw new UnauthorizedException("Invalid or expired refresh token.");

        if (customer.TenantId != tenant.Id)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        // Rotate: mark old token used, issue new one
        stored.MarkUsed();
        refreshTokenRepository.Update(stored);

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var newRefreshToken = CustomerRefreshToken.Create(customer.Id, tokenHash, CustomerAuthConstants.RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateCustomerAccessToken(customer);
        return new CustomerAuthTokenDto(accessToken, rawToken, customer.ToDto());
    }
}
