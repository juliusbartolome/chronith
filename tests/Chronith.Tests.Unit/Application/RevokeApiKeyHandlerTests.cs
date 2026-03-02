using Chronith.Application.Commands.ApiKeys;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

/// <summary>
/// Unit tests for <see cref="RevokeApiKeyHandler"/>.
/// </summary>
public sealed class RevokeApiKeyHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid KeyId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (
        RevokeApiKeyHandler Handler,
        IApiKeyRepository ApiKeyRepo,
        IUnitOfWork UnitOfWork)
        Build()
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);

        var apiKeyRepo = Substitute.For<IApiKeyRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var handler = new RevokeApiKeyHandler(tenantCtx, apiKeyRepo, unitOfWork);

        return (handler, apiKeyRepo, unitOfWork);
    }

    [Fact]
    public async Task Handle_RevokesKey_WhenFound()
    {
        // Arrange
        var (handler, apiKeyRepo, unitOfWork) = Build();
        var cmd = new RevokeApiKeyCommand(KeyId);

        var key = new TenantApiKey
        {
            Id = KeyId,
            TenantId = TenantId,
            KeyHash = "abc123",
            Description = "Test",
            Role = "Admin",
        };

        apiKeyRepo
            .GetByIdAsync(KeyId, TenantId, Arg.Any<CancellationToken>())
            .Returns(key);

        TenantApiKey? updatedKey = null;
        await apiKeyRepo.UpdateAsync(
            Arg.Do<TenantApiKey>(k => updatedKey = k),
            Arg.Any<CancellationToken>());

        // Act
        await handler.Handle(cmd, CancellationToken.None);

        // Assert
        updatedKey.Should().NotBeNull();
        updatedKey!.IsRevoked.Should().BeTrue();
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ThrowsNotFoundException_WhenKeyNotFound()
    {
        // Arrange
        var (handler, apiKeyRepo, _) = Build();
        var cmd = new RevokeApiKeyCommand(KeyId);

        apiKeyRepo
            .GetByIdAsync(KeyId, TenantId, Arg.Any<CancellationToken>())
            .Returns((TenantApiKey?)null);

        // Act
        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
