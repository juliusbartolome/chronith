using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class WaitlistEntryEntityMapper
{
    public static WaitlistEntry ToDomain(WaitlistEntryEntity entity)
    {
        var domain = new WaitlistEntry();
        SetProperty(domain, nameof(WaitlistEntry.Id), entity.Id);
        SetProperty(domain, nameof(WaitlistEntry.TenantId), entity.TenantId);
        SetProperty(domain, nameof(WaitlistEntry.BookingTypeId), entity.BookingTypeId);
        SetProperty(domain, nameof(WaitlistEntry.StaffMemberId), entity.StaffMemberId);
        SetProperty(domain, nameof(WaitlistEntry.CustomerId), entity.CustomerId);
        SetProperty(domain, nameof(WaitlistEntry.CustomerEmail), entity.CustomerEmail);
        SetProperty(domain, nameof(WaitlistEntry.DesiredStart), entity.DesiredStart);
        SetProperty(domain, nameof(WaitlistEntry.DesiredEnd), entity.DesiredEnd);
        SetProperty(domain, nameof(WaitlistEntry.Status), entity.Status);
        SetProperty(domain, nameof(WaitlistEntry.OfferedAt), entity.OfferedAt);
        SetProperty(domain, nameof(WaitlistEntry.ExpiresAt), entity.ExpiresAt);
        SetProperty(domain, nameof(WaitlistEntry.CreatedAt), entity.CreatedAt);
        SetProperty(domain, nameof(WaitlistEntry.IsDeleted), entity.IsDeleted);
        return domain;
    }

    public static WaitlistEntryEntity ToEntity(WaitlistEntry domain)
        => new()
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            BookingTypeId = domain.BookingTypeId,
            StaffMemberId = domain.StaffMemberId,
            CustomerId = domain.CustomerId,
            CustomerEmail = domain.CustomerEmail,
            DesiredStart = domain.DesiredStart,
            DesiredEnd = domain.DesiredEnd,
            Status = domain.Status,
            OfferedAt = domain.OfferedAt,
            ExpiresAt = domain.ExpiresAt,
            CreatedAt = domain.CreatedAt,
            IsDeleted = domain.IsDeleted
        };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        prop?.SetValue(target, value);
    }
}
