using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;

namespace Chronith.Infrastructure.Services.Audit;

public sealed class TenantSnapshotResolver(
    ITenantRepository tenantRepo) : IAuditSnapshotResolver
{
    public string EntityType => "Tenant";

    public async Task<string?> ResolveSnapshotAsync(Guid entityId, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetByIdAsync(entityId, ct);
        if (tenant is null)
            return null;

        return System.Text.Json.JsonSerializer.Serialize(tenant.ToDto());
    }
}
