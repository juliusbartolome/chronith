using Chronith.Application.Commands.CustomerAuth.MagicLink;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application.CustomerAuth;

public sealed class CustomerMagicLinkRegisterCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string TenantSlug = "test-tenant";

    private static (
        CustomerMagicLinkRegisterCommandHandler Handler,
        ITenantRepository TenantRepo,
        ITenantAuthConfigRepository AuthConfigRepo,
        ICustomerRepository CustomerRepo,
        ITokenService TokenService,
        IEnumerable<INotificationChannel> NotificationChannels,
        INotificationChannel EmailChannel,
        IUnitOfWork UnitOfWork)
        Build()
    {
        var tenantRepo = Substitute.For<ITenantRepository>();
        var authConfigRepo = Substitute.For<ITenantAuthConfigRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var tokenService = Substitute.For<ITokenService>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var emailChannel = Substitute.For<INotificationChannel>();
        emailChannel.ChannelType.Returns("email");

        var channels = new List<INotificationChannel> { emailChannel };

        tokenService.CreateMagicLinkToken(Arg.Any<Customer>(), Arg.Any<string>())
            .Returns("magic-link-token");

        var handler = new CustomerMagicLinkRegisterCommandHandler(
            tenantRepo, authConfigRepo, customerRepo,
            tokenService, channels, unitOfWork);

        return (handler, tenantRepo, authConfigRepo, customerRepo,
            tokenService, channels, emailChannel, unitOfWork);
    }

    private static Tenant BuildTenant() =>
        Tenant.Create(TenantSlug, "Test Tenant", "Asia/Manila");

    private static TenantAuthConfig BuildAuthConfig(Guid tenantId, bool magicLinkEnabled = true)
    {
        var config = TenantAuthConfig.Create(tenantId);
        config.Update(allowBuiltInAuth: true, null, null, null, magicLinkEnabled: magicLinkEnabled);
        return config;
    }

    [Fact]
    public async Task Handle_CreatesCustomer_WithMagicLinkProvider()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, _, _, _, unitOfWork) = Build();
        var tenant = BuildTenant();
        var authConfig = BuildAuthConfig(tenant.Id, magicLinkEnabled: true);

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(authConfig);
        customerRepo.GetByEmailAsync(tenant.Id, "test@example.com", Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        var command = new CustomerMagicLinkRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Name = "Test User"
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: customer saved with magic-link provider and not email-verified
        await customerRepo.Received(1).AddAsync(
            Arg.Is<Customer>(c =>
                c.AuthProvider == "magic-link" &&
                c.IsEmailVerified == false &&
                c.Email == "test@example.com" &&
                c.Name == "Test User" &&
                c.PasswordHash == null),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SendsEmail_WithVerificationLink()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, _, _, emailChannel, _) = Build();
        var tenant = BuildTenant();
        var authConfig = BuildAuthConfig(tenant.Id, magicLinkEnabled: true);

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(authConfig);
        customerRepo.GetByEmailAsync(tenant.Id, "test@example.com", Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        var command = new CustomerMagicLinkRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Name = "Test User"
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert: the email channel's SendAsync was called
        await emailChannel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m => m.Recipient == "test@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsMagicLinkInitiatedDto()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, _, _, _, _) = Build();
        var tenant = BuildTenant();
        var authConfig = BuildAuthConfig(tenant.Id, magicLinkEnabled: true);

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(authConfig);
        customerRepo.GetByEmailAsync(tenant.Id, "test@example.com", Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        var command = new CustomerMagicLinkRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Name = "Test User"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<MagicLinkInitiatedDto>();
        result.Message.Should().Be("Check your email to complete registration.");
    }

    [Fact]
    public async Task Handle_Throws_WhenMagicLinkNotEnabled()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, _, _, _, _, _) = Build();
        var tenant = BuildTenant();
        var authConfig = BuildAuthConfig(tenant.Id, magicLinkEnabled: false);

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(authConfig);

        var command = new CustomerMagicLinkRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Name = "Test User"
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*magic link*");
    }

    [Fact]
    public async Task Handle_Throws_WhenEmailAlreadyExists()
    {
        // Arrange
        var (handler, tenantRepo, authConfigRepo, customerRepo, _, _, _, _) = Build();
        var tenant = BuildTenant();
        var authConfig = BuildAuthConfig(tenant.Id, magicLinkEnabled: true);

        tenantRepo.GetBySlugAsync(TenantSlug, Arg.Any<CancellationToken>()).Returns(tenant);
        authConfigRepo.GetByTenantIdAsync(tenant.Id, Arg.Any<CancellationToken>()).Returns(authConfig);

        var existingCustomer = Customer.Create(tenant.Id, "test@example.com", null, "Existing", null, "magic-link");
        customerRepo.GetByEmailAsync(tenant.Id, "test@example.com", Arg.Any<CancellationToken>())
            .Returns(existingCustomer);

        var command = new CustomerMagicLinkRegisterCommand
        {
            TenantSlug = TenantSlug,
            Email = "test@example.com",
            Name = "Test User"
        };

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already exists*");
    }
}
