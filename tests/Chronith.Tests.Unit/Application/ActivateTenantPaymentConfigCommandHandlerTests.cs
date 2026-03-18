using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class ActivateTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_ApiType_DeactivatesOthersFirstThenActivates()
    {
        var id = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new ActivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        await handler.Handle(new ActivateTenantPaymentConfigCommand(id), CancellationToken.None);

        await _repo.Received(1).DeactivateAllByProviderNameAsync(
            config.TenantId, "PayMongo", Arg.Any<CancellationToken>());
        config.IsActive.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(config, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManualType_ActivatesWithoutDeactivatingOthers()
    {
        var id = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(Guid.NewGuid(), "Manual", "Cash", "{}", null, null);
        config.Deactivate(); // start inactive
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new ActivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        await handler.Handle(new ActivateTenantPaymentConfigCommand(id), CancellationToken.None);

        await _repo.DidNotReceive().DeactivateAllByProviderNameAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        config.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(default(TenantPaymentConfig));

        var handler = new ActivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var act = () => handler.Handle(
            new ActivateTenantPaymentConfigCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
