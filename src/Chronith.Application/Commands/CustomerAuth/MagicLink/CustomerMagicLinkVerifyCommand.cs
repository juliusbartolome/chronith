using Chronith.Application.Constants;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.CustomerAuth.MagicLink;

public sealed record CustomerMagicLinkVerifyCommand : IRequest<CustomerAuthTokenDto>
{
    public required string TenantSlug { get; init; }
    public required string Token { get; init; }
}

public sealed class CustomerMagicLinkVerifyCommandValidator : AbstractValidator<CustomerMagicLinkVerifyCommand>
{
    public CustomerMagicLinkVerifyCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class CustomerMagicLinkVerifyCommandHandler(
    ICustomerRepository customerRepository,
    ICustomerRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CustomerMagicLinkVerifyCommand, CustomerAuthTokenDto>
{
    public async Task<CustomerAuthTokenDto> Handle(
        CustomerMagicLinkVerifyCommand request, CancellationToken cancellationToken)
    {
        // Step 1: Validate the magic link token — throws UnauthorizedException on failure
        var customerId = tokenService.ValidateMagicLinkToken(request.Token, request.TenantSlug);

        // Step 2: Load the customer cross-tenant
        var customer = await customerRepository.GetByIdAcrossTenantsAsync(customerId, cancellationToken)
            ?? throw new NotFoundException("Customer", customerId.ToString());

        // Step 3: Mark email as verified
        customer.MarkEmailVerified();
        customerRepository.Update(customer);

        // Step 4: Issue tokens
        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = CustomerRefreshToken.Create(customer.Id, tokenHash, CustomerAuthConstants.RefreshTokenTtl);
        await refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        // Step 5: Persist
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Step 6: Return DTO
        var accessToken = tokenService.CreateCustomerAccessToken(customer);
        return new CustomerAuthTokenDto(accessToken, rawToken, customer.ToDto());
    }
}
