using Chronith.Application.Commands.CustomerAuth.OidcLogin;
using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application.CustomerAuth;

public sealed class CustomerOidcLoginCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Tenant TestTenant = Tenant.Create("test-tenant", "Test Tenant", "Asia/Manila");

    private static TenantAuthConfig CreateOidcConfig()
    {
        var config = TenantAuthConfig.Create(TenantId);
        config.Update(true, "https://accounts.google.com", "client-id-123", "client-id-123", false);
        return config;
    }

    private static (
        CustomerOidcLoginCommandHandler Handler,
        ITenantRepository TenantRepo,
        ITenantAuthConfigRepository AuthConfigRepo,
        ICustomerRepository CustomerRepo,
        ICustomerRefreshTokenRepository RefreshTokenRepo,
        IOidcTokenValidator OidcValidator,
        ITokenService TokenService,
        IUnitOfWork UnitOfWork)
        Build()
    {
        var tenantRepo = Substitute.For<ITenantRepository>();
        var authConfigRepo = Substitute.For<ITenantAuthConfigRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var refreshTokenRepo = Substitute.For<ICustomerRefreshTokenRepository>();
        var oidcValidator = Substitute.For<IOidcTokenValidator>();
        var tokenService = Substitute.For<ITokenService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        tokenService.CreateRefreshToken().Returns(("raw-token", "hashed-token"));
        tokenService.CreateCustomerAccessToken(Arg.Any<Customer>()).Returns("access-token");

        var handler = new CustomerOidcLoginCommandHandler(
            tenantRepo, authConfigRepo, customerRepo, refreshTokenRepo,
            oidcValidator, tokenService, unitOfWork);

        return (handler, tenantRepo, authConfigRepo, customerRepo, refreshTokenRepo,
            oidcValidator, tokenService, unitOfWork);
    }

    [Fact]
    public async Task Handle_NewCustomer_AutoCreatesAndReturnsTokens()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, _, oidcValidator, _, unitOfWork) = Build();
        var command = new CustomerOidcLoginCommand("test-tenant", "oidc-id-token");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        authConfigRepo.GetByTenantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CreateOidcConfig());
        oidcValidator.ValidateAsync("oidc-id-token", "https://accounts.google.com",
                "client-id-123", "client-id-123", Arg.Any<CancellationToken>())
            .Returns(new OidcValidationResult(true, "ext-sub-123", "oidc@example.com", "OIDC User", null));
        customerRepo.GetByExternalIdAsync(Arg.Any<Guid>(), "ext-sub-123", Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-token");
        result.Customer.Email.Should().Be("oidc@example.com");
        result.Customer.Name.Should().Be("OIDC User");

        await customerRepo.Received(1).AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingCustomer_UpdatesLoginAndReturnsTokens()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, _, oidcValidator, _, unitOfWork) = Build();
        var existingCustomer = Customer.CreateOidc(TenantId, "oidc@example.com", "OIDC User",
            "ext-sub-123", "https://accounts.google.com");
        var command = new CustomerOidcLoginCommand("test-tenant", "oidc-id-token");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        authConfigRepo.GetByTenantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CreateOidcConfig());
        oidcValidator.ValidateAsync("oidc-id-token", "https://accounts.google.com",
                "client-id-123", "client-id-123", Arg.Any<CancellationToken>())
            .Returns(new OidcValidationResult(true, "ext-sub-123", "oidc@example.com", "OIDC User", null));
        customerRepo.GetByExternalIdAsync(Arg.Any<Guid>(), "ext-sub-123", Arg.Any<CancellationToken>())
            .Returns(existingCustomer);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        result.Customer.Email.Should().Be("oidc@example.com");

        // Should update, not add
        customerRepo.Received(1).Update(Arg.Any<Customer>());
        await customerRepo.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, _, _, oidcValidator, _, _) = Build();
        var command = new CustomerOidcLoginCommand("test-tenant", "invalid-token");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        authConfigRepo.GetByTenantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CreateOidcConfig());
        oidcValidator.ValidateAsync("invalid-token", "https://accounts.google.com",
                "client-id-123", "client-id-123", Arg.Any<CancellationToken>())
            .Returns(new OidcValidationResult(false, null, null, null, "Token signature invalid"));

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Token signature invalid");
    }

    [Fact]
    public async Task Handle_OidcNotConfigured_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, _, _, _, _, _) = Build();
        var command = new CustomerOidcLoginCommand("test-tenant", "oidc-id-token");

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        authConfigRepo.GetByTenantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((TenantAuthConfig?)null);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*OIDC is not configured*");
    }

    [Fact]
    public async Task Handle_OidcMissingIssuer_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, _, _, _, _, _) = Build();
        var command = new CustomerOidcLoginCommand("test-tenant", "oidc-id-token");

        // Config with no OIDC issuer set
        var config = TenantAuthConfig.Create(TenantId);
        // AllowBuiltInAuth but no OIDC settings

        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(TestTenant);
        authConfigRepo.GetByTenantIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(config);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*OIDC is not configured*");
    }
}
