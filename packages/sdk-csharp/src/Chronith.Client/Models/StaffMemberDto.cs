namespace Chronith.Client.Models;

public sealed record StaffMemberDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Email,
    string? Phone,
    bool IsActive,
    DateTimeOffset CreatedAt
);
