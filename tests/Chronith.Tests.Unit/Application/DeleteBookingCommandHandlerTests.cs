using Chronith.Application.Commands.Bookings;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class DeleteBookingCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static (DeleteBookingHandler Handler, IBookingRepository Repo, IUnitOfWork UoW, Guid BookingId) Build(
        BookingStatus status = BookingStatus.PendingPayment)
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);

        var booking = new BookingBuilder().InStatus(status).Build();

        var repo = Substitute.For<IBookingRepository>();
        repo.GetByIdAsync(TenantId, booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        var unitOfWork = Substitute.For<IUnitOfWork>();

        var handler = new DeleteBookingHandler(tenantCtx, repo, unitOfWork);
        return (handler, repo, unitOfWork, booking.Id);
    }

    [Fact]
    public async Task Handle_WhenBookingExists_CallsUpdateAndSaves()
    {
        var (handler, repo, unitOfWork, bookingId) = Build();

        await handler.Handle(new DeleteBookingCommand(bookingId), CancellationToken.None);

        await repo.Received(1).UpdateAsync(Arg.Any<Chronith.Domain.Models.Booking>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBookingExists_SoftDeletesTheBooking()
    {
        var (handler, repo, _, bookingId) = Build();

        await handler.Handle(new DeleteBookingCommand(bookingId), CancellationToken.None);

        await repo.Received(1).UpdateAsync(
            Arg.Is<Chronith.Domain.Models.Booking>(b => b.IsDeleted),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(BookingStatus.PendingPayment)]
    [InlineData(BookingStatus.PendingVerification)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Cancelled)]
    public async Task Handle_WorksForAllBookingStatuses(BookingStatus status)
    {
        var (handler, repo, unitOfWork, bookingId) = Build(status);

        var act = () => handler.Handle(new DeleteBookingCommand(bookingId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await repo.Received(1).UpdateAsync(Arg.Any<Chronith.Domain.Models.Booking>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBookingNotFound_ThrowsNotFoundException()
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);

        var repo = Substitute.For<IBookingRepository>();
        repo.GetByIdAsync(TenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(default(Chronith.Domain.Models.Booking));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new DeleteBookingHandler(tenantCtx, repo, unitOfWork);

        var act = () => handler.Handle(
            new DeleteBookingCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenNotFound_DoesNotCallSaveChanges()
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);

        var repo = Substitute.For<IBookingRepository>();
        repo.GetByIdAsync(TenantId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(default(Chronith.Domain.Models.Booking));

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new DeleteBookingHandler(tenantCtx, repo, unitOfWork);

        try { await handler.Handle(new DeleteBookingCommand(Guid.NewGuid()), CancellationToken.None); }
        catch (NotFoundException) { }

        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
