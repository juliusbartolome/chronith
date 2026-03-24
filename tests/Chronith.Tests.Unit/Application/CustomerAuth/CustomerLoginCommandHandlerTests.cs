using Chronith.Application.Commands.CustomerAuth.Login;
using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application.CustomerAuth;

public sealed class CustomerLoginCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Tenant TestTenant = Tenant.Create("test-tenant", "Test Tenant", "Asia/Manila");

    private static (
        CustomerLoginCommandHandler Handler,
        ITenantRepository TenantRepo,
        ICustomerRepository CustomerRepo,
        ICustomerRefreshTokenRepository RefreshTokenRepo,
        ITokenService TokenService,
        IPasswordHasher PasswordHasher,
        IUnitOfWork UnitOfWork)
        Build()
    {
        var tenantRepo = Substitute.For<ITenantRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var refreshTokenRepo = Substitute.For<ICustomerRefreshTokenRepository>();
        var tokenService = Substitute.For<ITokenService>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        tokenService.CreateRefreshToken().Returns(("raw-token", "hashed-token"));
        tokenService.CreateCustomerAccessToken(Arg.Any<Customer>()).Returns("access-token");

        passwordHasher.Verify("Password1", "hashed-password").Returns(true);

        var handler = new CustomerLoginCommandHandler(
            tenantRepo, customerRepo, refreshTokenRepo, tokenService, passwordHasher, unitOfWork);

        return (handler, tenantRepo, customerRepo, refreshTokenRepo, tokenService, passwordHasher, unitOfWork);
    }

    private static Customer CreateActiveCustomer(string email = "customer@example.com", string password = "Password1")
    {
        return Customer.Create(TenantId, email, "hashed-password", "Jane", "Doe", null, "builtin");
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsTokensAndCustomer()
    {
        // Arrange
        var (handler, tenantRepo, customerRepo, _, _, _, unitOfWork) = Build();
        var customer = CreateActiveCustomer();
        var command = new CustomerLoginCommand("test-tenant", "customer@example.com", "Password1");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        customerRepo.GetByEmailAsync(Arg.Any<Guid>(), "customer@example.com", Arg.Any<CancellationToken>())
            .Returns(customer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-token");
        result.Customer.Email.Should().Be("customer@example.com");
        customerRepo.Received(1).Update(Arg.Any<Customer>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, customerRepo, _, _, _, _) = Build();
        var customer = CreateActiveCustomer(password: "CorrectPassword1");
        var command = new CustomerLoginCommand("test-tenant", "customer@example.com", "WrongPassword1");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        customerRepo.GetByEmailAsync(Arg.Any<Guid>(), "customer@example.com", Arg.Any<CancellationToken>())
            .Returns(customer);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_InactiveCustomer_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, customerRepo, _, _, _, _) = Build();
        var customer = CreateActiveCustomer();
        customer.Deactivate();
        var command = new CustomerLoginCommand("test-tenant", "customer@example.com", "Password1");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        customerRepo.GetByEmailAsync(Arg.Any<Guid>(), "customer@example.com", Arg.Any<CancellationToken>())
            .Returns(customer);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_CustomerNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, customerRepo, _, _, _, _) = Build();
        var command = new CustomerLoginCommand("test-tenant", "unknown@example.com", "Password1");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        customerRepo.GetByEmailAsync(Arg.Any<Guid>(), "unknown@example.com", Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_TenantNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, _, _, _, _, _) = Build();
        var command = new CustomerLoginCommand("nonexistent", "customer@example.com", "Password1");

        tenantRepo.GetBySlugAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
    }

    [Fact]
    public async Task Handle_OidcCustomerNoPasswordHash_ThrowsUnauthorizedException()
    {
        // Arrange: OIDC customers have null PasswordHash
        var (handler, tenantRepo, customerRepo, _, _, _, _) = Build();
        var customer = Customer.CreateOidc(TenantId, "oidc@example.com", "OIDC User", "ext-123", "https://issuer.example.com");
        var command = new CustomerLoginCommand("test-tenant", "oidc@example.com", "Password1");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        customerRepo.GetByEmailAsync(Arg.Any<Guid>(), "oidc@example.com", Arg.Any<CancellationToken>())
            .Returns(customer);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid credentials.");
    }
}
