namespace Chronith.Application.DTOs;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string TimeZoneId,
    string Slug
);
