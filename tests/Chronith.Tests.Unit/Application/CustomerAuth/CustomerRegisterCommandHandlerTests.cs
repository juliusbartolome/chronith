using Chronith.Application.Commands.CustomerAuth.Register;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application.CustomerAuth;

public sealed class CustomerRegisterCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string TenantSlug = "test-tenant";

    private static (
        CustomerRegisterCommandHandler Handler,
        ITenantRepository TenantRepo,
        ITenantAuthConfigRepository AuthConfigRepo,
        ICustomerRepository CustomerRepo,
        ICustomerRefreshTokenRepository RefreshTokenRepo,
        ITokenService TokenService,
        IUnitOfWork UnitOfWork)
        Build()
    {
        var tenantRepo = Substitute.For<ITenantRepository>();
        var authConfigRepo = Substitute.For<ITenantAuthConfigRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var refreshTokenRepo = Substitute.For<ICustomerRefreshTokenRepository>();
        var tokenService = Substitute.For<ITokenService>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        tokenService.CreateRefreshToken().Returns(("raw-token", "token-hash"));
        tokenService.CreateCustomerAccessToken(Arg.Any<Customer>()).Returns("access-token");
        passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");

        var handler = new CustomerRegisterCommandHandler(
            tenantRepo, authConfigRepo, customerRepo,
            refreshTokenRepo, tokenService, passwordHasher, unitOfWork);

        return (handler, tenantRepo, authConfigRepo, customerRepo,
            refreshTokenRepo, tokenService, unitOfWork);
    }

    private static Tenant BuildTenant()
    {
        return Tenant.Create(TenantSlug, "Test Tenant", "Asia/Manila");
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsCustomerAuthTokenDto()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, refreshTokenRepo, tokenService, unitOfWork) = Build();
        var tenant = BuildTenant();

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns((TenantAuthConfig?)null);
        customerRepo.GetByEmailAsync(tenant.Id, "test@example.com", Arg.Any<CancellationToken>()).Returns((Customer?)null);

        var command = new CustomerRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Password = "Password1",
            Name = "Test User"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-token");
        result.Customer.Email.Should().Be("test@example.com");
        result.Customer.Name.Should().Be("Test User");
        result.Customer.AuthProvider.Should().Be("builtin");

        await customerRepo.Received(1).AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
        await refreshTokenRepo.Received(1).AddAsync(Arg.Any<CustomerRefreshToken>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflictException()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, _, _, _) = Build();
        var tenant = BuildTenant();

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns((TenantAuthConfig?)null);

        var existingCustomer = Customer.Create(tenant.Id, "test@example.com", "hash", "Existing", null, "builtin");
        customerRepo.GetByEmailAsync(tenant.Id, "test@example.com", Arg.Any<CancellationToken>()).Returns(existingCustomer);

        var command = new CustomerRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Password = "Password1",
            Name = "Test User"
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_BuiltInAuthDisabled_ThrowsUnauthorizedException()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, _, _, _, _) = Build();
        var tenant = BuildTenant();

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);

        var authConfig = TenantAuthConfig.Create(tenant.Id);
        authConfig.Update(allowBuiltInAuth: false, null, null, null, false);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(authConfig);

        var command = new CustomerRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Password = "Password1",
            Name = "Test User"
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Built-in authentication is disabled*");
    }

    [Fact]
    public async Task Handle_MagicLinkEnabled_ThrowsInvalidOperationException()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, _, _, _, _) = Build();
        var tenant = BuildTenant();

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);

        var authConfig = TenantAuthConfig.Create(tenant.Id);
        authConfig.Update(allowBuiltInAuth: true, null, null, null, magicLinkEnabled: true);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(authConfig);

        var command = new CustomerRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Password = "Password1",
            Name = "Test User"
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*magic-link*");
    }

    [Fact]
    public async Task Handle_TenantNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var (handler, tenantRepo, _, _, _, _, _) = Build();
        tenantRepo.GetBySlugAsync("nonexistent", Arg.Any<CancellationToken>()).Returns((Tenant?)null);

        var command = new CustomerRegisterCommand
        {
            TenantSlug = "nonexistent",
            Email = "test@example.com",
            Password = "Password1",
            Name = "Test User"
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
