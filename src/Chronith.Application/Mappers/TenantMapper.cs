using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantMapper
{
    public static TenantDto ToDto(this Tenant tenant) =>
        new(
            Id: tenant.Id,
            Name: tenant.Name,
            TimeZoneId: tenant.TimeZoneId,
            Slug: tenant.Slug
        );
}
