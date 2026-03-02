using Chronith.Application.Commands.ApiKeys;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="CreateApiKeyHandler"/>.
/// </summary>
public sealed class CreateApiKeyHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (
        CreateApiKeyHandler Handler,
        IApiKeyRepository ApiKeyRepo,
        IUnitOfWork UnitOfWork)
        Build()
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);

        var apiKeyRepo = Substitute.For<IApiKeyRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var handler = new CreateApiKeyHandler(tenantCtx, apiKeyRepo, unitOfWork);

        return (handler, apiKeyRepo, unitOfWork);
    }

    [Fact]
    public async Task Handle_CreatesKeyWithCorrectTenantId()
    {
        // Arrange
        var (handler, apiKeyRepo, _) = Build();
        var cmd = new CreateApiKeyCommand { Description = "Test Key", Role = "Admin" };

        TenantApiKey? captured = null;
        await apiKeyRepo.AddAsync(
            Arg.Do<TenantApiKey>(k => captured = k),
            Arg.Any<CancellationToken>());

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.Description.Should().Be("Test Key");
        captured.Role.Should().Be("Admin");
        result.RawKey.Should().StartWith("cth_");
    }

    [Fact]
    public async Task Handle_CallsSaveChanges()
    {
        // Arrange
        var (handler, _, unitOfWork) = Build();
        var cmd = new CreateApiKeyCommand { Description = "Key", Role = "ReadOnly" };

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
