using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;

namespace Chronith.Infrastructure.Services.Audit;

public sealed class BookingTypeSnapshotResolver(
    IBookingTypeRepository bookingTypeRepo,
    ITenantContext tenantContext) : IAuditSnapshotResolver
{
    public string EntityType => "BookingType";

    public async Task<string?> ResolveSnapshotAsync(Guid entityId, CancellationToken ct)
    {
        var bookingType = await bookingTypeRepo.GetByIdAsync(tenantContext.TenantId, entityId, ct);
        if (bookingType is null)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(bookingType.ToDto());
    }
}
