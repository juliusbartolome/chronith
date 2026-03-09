using Chronith.Application.Constants;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Register;

public sealed class CustomerRegisterCommandHandler(
    ITenantRepository tenantRepository,
    ITenantAuthConfigRepository authConfigRepository,
    ICustomerRepository customerRepository,
    ICustomerRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
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

        var existing = await customerRepository.GetByEmailAsync(tenant.Id, request.Email, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"A customer with email '{request.Email}' already exists.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
        var customer = Customer.Create(tenant.Id, request.Email, passwordHash, request.Name,
            request.Phone, "builtin");

        await customerRepository.AddAsync(customer, cancellationToken);

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = CustomerRefreshToken.Create(customer.Id, tokenHash, CustomerAuthConstants.RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateCustomerAccessToken(customer);
        return new CustomerAuthTokenDto(accessToken, rawToken, customer.ToDto());
    }
}
