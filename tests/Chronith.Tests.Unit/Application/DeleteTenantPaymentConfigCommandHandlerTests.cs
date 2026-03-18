using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class DeleteTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WhenExists_SoftDeletes()
    {
        var id = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new DeleteTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        await handler.Handle(new DeleteTenantPaymentConfigCommand(id), CancellationToken.None);

        await _repo.Received(1).SoftDeleteAsync(id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns(default(TenantPaymentConfig));

        var handler = new DeleteTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var act = () => handler.Handle(
            new DeleteTenantPaymentConfigCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
