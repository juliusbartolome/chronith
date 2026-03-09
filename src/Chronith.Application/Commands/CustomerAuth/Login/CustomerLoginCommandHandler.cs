using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.Login;

public sealed class CustomerLoginCommandHandler(
    ITenantRepository tenantRepository,
    ICustomerRepository customerRepository,
    ICustomerRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CustomerLoginCommand, CustomerAuthTokenDto>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromDays(30);

    public async Task<CustomerAuthTokenDto> Handle(
        CustomerLoginCommand request, CancellationToken cancellationToken)
    {
        const string invalidCredentials = "Invalid credentials.";

        var tenant = await tenantRepository.GetBySlugAsync(request.TenantSlug, cancellationToken)
            ?? throw new UnauthorizedException(invalidCredentials);

        var customer = await customerRepository.GetByEmailAsync(tenant.Id, request.Email, cancellationToken)
            ?? throw new UnauthorizedException(invalidCredentials);

        if (!customer.IsActive)
            throw new UnauthorizedException(invalidCredentials);

        if (customer.PasswordHash is null ||
            !BCrypt.Net.BCrypt.Verify(request.Password, customer.PasswordHash))
            throw new UnauthorizedException(invalidCredentials);

        customer.RecordLogin();
        customerRepository.Update(customer);

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = CustomerRefreshToken.Create(customer.Id, tokenHash, RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenService.CreateCustomerAccessToken(customer);
        return new CustomerAuthTokenDto(accessToken, rawToken, customer.ToDto());
    }
}
