using Chronith.Domain.Models;

namespace Chronith.Tests.Unit.Helpers;

public static class StaffMemberBuilder
{
    public static StaffMember Build(
        Guid? tenantId = null,
        Guid? tenantUserId = null,
        string name = "Test Staff",
        string email = "staff@example.com",
        IReadOnlyList<StaffAvailabilityWindow>? windows = null)
        => StaffMember.Create(
            tenantId ?? Guid.NewGuid(),
            tenantUserId,
            name,
            email,
            windows ?? []);
}
