using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TimeBlockEntityMapper
{
    public static TimeBlock ToDomain(TimeBlockEntity entity)
    {
        var domain = new TimeBlock();
        SetProperty(domain, nameof(TimeBlock.Id), entity.Id);
        SetProperty(domain, nameof(TimeBlock.TenantId), entity.TenantId);
        SetProperty(domain, nameof(TimeBlock.BookingTypeId), entity.BookingTypeId);
        SetProperty(domain, nameof(TimeBlock.StaffMemberId), entity.StaffMemberId);
        SetProperty(domain, nameof(TimeBlock.Start), entity.Start);
        SetProperty(domain, nameof(TimeBlock.End), entity.End);
        SetProperty(domain, nameof(TimeBlock.Reason), entity.Reason);
        SetProperty(domain, nameof(TimeBlock.IsDeleted), entity.IsDeleted);
        SetProperty(domain, nameof(TimeBlock.CreatedAt), entity.CreatedAt);
        return domain;
    }

    public static TimeBlockEntity ToEntity(TimeBlock domain)
        => new()
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            BookingTypeId = domain.BookingTypeId,
            StaffMemberId = domain.StaffMemberId,
            Start = domain.Start,
            End = domain.End,
            Reason = domain.Reason,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt
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
