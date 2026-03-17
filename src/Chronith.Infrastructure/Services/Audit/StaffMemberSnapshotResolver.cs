using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;

namespace Chronith.Infrastructure.Services.Audit;

public sealed class StaffMemberSnapshotResolver(
    IStaffMemberRepository staffRepo,
    ITenantContext tenantContext) : IAuditSnapshotResolver
{
    public string EntityType => "StaffMember";

    public async Task<string?> ResolveSnapshotAsync(Guid entityId, CancellationToken ct)
    {
        var staff = await staffRepo.GetByIdAsync(tenantContext.TenantId, entityId, ct);
        if (staff is null)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(staff.ToDto());
    }
}
