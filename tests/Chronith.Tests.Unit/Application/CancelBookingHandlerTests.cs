using Chronith.Application.Commands.Bookings;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CancelBookingHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static (CancelBookingHandler Handler, IBookingMetrics Metrics, Guid BookingId) Build()
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);
        tenantCtx.UserId.Returns("admin");
        tenantCtx.Role.Returns("Admin");

        var booking = new BookingBuilder()
            .InStatus(BookingStatus.PendingVerification)
            .Build();

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo
            .GetByIdAsync(TenantId, booking.Id, Arg.Any<CancellationToken>())
            .Returns(booking);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();
        var metrics = Substitute.For<IBookingMetrics>();

        var handler = new CancelBookingHandler(tenantCtx, bookingRepo, unitOfWork, publisher, metrics);
        return (handler, metrics, booking.Id);
    }

    [Fact]
    public async Task Handle_RecordsBookingCancelledMetric()
    {
        var (handler, metrics, bookingId) = Build();

        await handler.Handle(
            new CancelBookingCommand { BookingId = bookingId, BookingTypeSlug = "test-type" },
            CancellationToken.None);

        metrics.Received(1).RecordBookingCancelled(Arg.Any<string>());
    }
}
