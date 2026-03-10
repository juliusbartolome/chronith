using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;

namespace Chronith.Infrastructure.Services.Audit;

public sealed class BookingSnapshotResolver(
    IBookingRepository bookingRepo,
    ITenantContext tenantContext) : IAuditSnapshotResolver
{
    public string EntityType => "Booking";

    public async Task<string?> ResolveSnapshotAsync(Guid entityId, CancellationToken ct)
    {
        var booking = await bookingRepo.GetByIdAsync(tenantContext.TenantId, entityId, ct);
        if (booking is null)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(booking.ToDto());
    }
}
