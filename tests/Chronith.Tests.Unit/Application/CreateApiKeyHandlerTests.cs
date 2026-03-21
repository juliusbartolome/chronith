using Chronith.Application.Commands.ApiKeys;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CreateApiKeyHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (CreateApiKeyHandler Handler, IApiKeyRepository ApiKeyRepo, IUnitOfWork UnitOfWork)
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
    public async Task Handle_CreatesKeyWithCorrectTenantIdAndScopes()
    {
        // Arrange
        var (handler, apiKeyRepo, _) = Build();
        var cmd = new CreateApiKeyCommand
        {
            Description = "Test Key",
            Scopes = [ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead],
        };

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
        captured.Scopes.Should().BeEquivalentTo([ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead]);
        result.RawKey.Should().StartWith("cth_");
        result.Scopes.Should().BeEquivalentTo([ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead]);
    }

    [Fact]
    public async Task Handle_CallsSaveChanges()
    {
        var (handler, _, unitOfWork) = Build();
        var cmd = new CreateApiKeyCommand
        {
            Description = "Key",
            Scopes = [ApiKeyScope.BookingsRead],
        };

        await handler.Handle(cmd, CancellationToken.None);

        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
