using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class UpdateTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WhenExists_UpdatesAndReturnsDto()
    {
        var id = Guid.NewGuid();
        var existing = TenantPaymentConfig.Create(Guid.NewGuid(), "PayMongo", "OldLabel", "{}", null, null);
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new UpdateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var cmd = new UpdateTenantPaymentConfigCommand
        {
            Id = id,
            Label = "NewLabel",
            Settings = """{"SecretKey":"sk_new"}""",
            PublicNote = null,
            QrCodeUrl = null
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Label.Should().Be("NewLabel");
        result.Settings.Should().Contain("sk_new");
        result.IsActive.Should().BeFalse(); // unchanged
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((TenantPaymentConfig?)null);

        var handler = new UpdateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var act = () => handler.Handle(
            new UpdateTenantPaymentConfigCommand
            {
                Id = Guid.NewGuid(), Label = "L", Settings = "{}", PublicNote = null, QrCodeUrl = null
            }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
