using Chronith.Application.Commands.CustomerAuth.MagicLink;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Chronith.Tests.Unit.Application.CustomerAuth;

public sealed class CustomerMagicLinkVerifyCommandHandlerTests
{
    private const string TenantSlug = "test-tenant";
    private const string ValidToken = "valid.jwt.token";

    private static (
        CustomerMagicLinkVerifyCommandHandler Handler,
        ICustomerRepository CustomerRepo,
        ICustomerRefreshTokenRepository RefreshTokenRepo,
        ITokenService TokenService,
        IUnitOfWork UnitOfWork)
        Build()
    {
        var customerRepo = Substitute.For<ICustomerRepository>();
        var refreshTokenRepo = Substitute.For<ICustomerRefreshTokenRepository>();
        var tokenService = Substitute.For<ITokenService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        tokenService.CreateCustomerAccessToken(Arg.Any<Customer>()).Returns("access-token");
        tokenService.CreateRefreshToken().Returns(("raw-refresh", "hash-refresh"));

        var handler = new CustomerMagicLinkVerifyCommandHandler(
            customerRepo, refreshTokenRepo, tokenService, unitOfWork);

        return (handler, customerRepo, refreshTokenRepo, tokenService, unitOfWork);
    }

    private static Customer BuildCustomer(Guid? tenantId = null) =>
        Customer.Create(
            tenantId: tenantId ?? Guid.NewGuid(),
            email: "alice@example.com",
            passwordHash: null,
            name: "Alice",
            phone: null,
            authProvider: "magic-link");

    [Fact]
    public async Task Handle_MarksEmailVerified_AndReturnsTokens_ForValidToken()
    {
        // Arrange
        var (handler, customerRepo, refreshTokenRepo, tokenService, unitOfWork) = Build();

        var customer = BuildCustomer();
        var customerId = customer.Id;

        tokenService.ValidateMagicLinkToken(ValidToken, TenantSlug).Returns(customerId);
        customerRepo.GetByIdAcrossTenantsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(customer);

        var command = new CustomerMagicLinkVerifyCommand
        {
            TenantSlug = TenantSlug,
            Token = ValidToken
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: email must be marked verified
        customer.IsEmailVerified.Should().BeTrue("MarkEmailVerified must be called");

        // Assert: customer updated and saved
        customerRepo.Received(1).Update(customer);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Assert: refresh token added
        await refreshTokenRepo.Received(1).AddAsync(
            Arg.Any<CustomerRefreshToken>(),
            Arg.Any<CancellationToken>());

        // Assert: DTO returned with access and refresh tokens
        result.Should().BeOfType<CustomerAuthTokenDto>();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh");
        result.Customer.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ThrowsUnauthorized_ForExpiredToken()
    {
        // Arrange
        var (handler, _, _, tokenService, _) = Build();

        tokenService.ValidateMagicLinkToken(ValidToken, TenantSlug)
            .Throws(new UnauthorizedException("Token expired."));

        var command = new CustomerMagicLinkVerifyCommand
        {
            TenantSlug = TenantSlug,
            Token = ValidToken
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ThrowsUnauthorized_ForWrongPurpose()
    {
        // Arrange
        var (handler, _, _, tokenService, _) = Build();

        tokenService.ValidateMagicLinkToken(ValidToken, TenantSlug)
            .Throws(new UnauthorizedException("Invalid token purpose."));

        var command = new CustomerMagicLinkVerifyCommand
        {
            TenantSlug = TenantSlug,
            Token = ValidToken
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ThrowsUnauthorized_ForWrongTenantSlug()
    {
        // Arrange
        var (handler, _, _, tokenService, _) = Build();

        tokenService.ValidateMagicLinkToken(ValidToken, TenantSlug)
            .Throws(new UnauthorizedException("Tenant slug mismatch."));

        var command = new CustomerMagicLinkVerifyCommand
        {
            TenantSlug = TenantSlug,
            Token = ValidToken
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ThrowsNotFound_WhenCustomerDoesNotExist()
    {
        // Arrange
        var (handler, customerRepo, _, tokenService, _) = Build();
        var customerId = Guid.NewGuid();

        tokenService.ValidateMagicLinkToken(ValidToken, TenantSlug).Returns(customerId);
        customerRepo.GetByIdAcrossTenantsAsync(customerId, Arg.Any<CancellationToken>())
            .Returns(null as Customer);

        var command = new CustomerMagicLinkVerifyCommand
        {
            TenantSlug = TenantSlug,
            Token = ValidToken
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
