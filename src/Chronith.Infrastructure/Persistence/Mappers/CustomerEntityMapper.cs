using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

internal static class CustomerEntityMapper
{
    public static CustomerEntity ToEntity(this Customer c) => new()
    {
        Id = c.Id,
        TenantId = c.TenantId,
        Email = c.Email,
        PasswordHash = c.PasswordHash,
        Name = c.Name,
        Phone = c.Phone,
        ExternalId = c.ExternalId,
        AuthProvider = c.AuthProvider,
        IsEmailVerified = c.IsEmailVerified,
        IsActive = c.IsActive,
        IsDeleted = c.IsDeleted,
        CreatedAt = c.CreatedAt,
        LastLoginAt = c.LastLoginAt
    };

    public static Customer ToDomain(this CustomerEntity e) =>
        Customer.Hydrate(
            e.Id, e.TenantId, e.Email, e.PasswordHash,
            e.Name, e.Phone, e.ExternalId, e.AuthProvider,
            e.IsEmailVerified, e.IsActive, e.IsDeleted,
            e.CreatedAt, e.LastLoginAt);

    public static CustomerRefreshTokenEntity ToEntity(this CustomerRefreshToken t) => new()
    {
        Id = t.Id,
        CustomerId = t.CustomerId,
        TokenHash = t.TokenHash,
        ExpiresAt = t.ExpiresAt,
        UsedAt = t.UsedAt,
        CreatedAt = t.CreatedAt
    };

    public static CustomerRefreshToken ToDomain(this CustomerRefreshTokenEntity e) =>
        CustomerRefreshToken.Hydrate(
            e.Id, e.CustomerId, e.TokenHash,
            e.ExpiresAt, e.UsedAt, e.CreatedAt);
}
