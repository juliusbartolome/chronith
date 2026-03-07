using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class StaffMemberEntityMapper
{
    public static StaffMember ToDomain(StaffMemberEntity entity)
    {
        var domain = new StaffMember();
        SetProperty(domain, nameof(StaffMember.Id), entity.Id);
        SetProperty(domain, nameof(StaffMember.TenantId), entity.TenantId);
        SetProperty(domain, nameof(StaffMember.TenantUserId), entity.TenantUserId);
        SetProperty(domain, nameof(StaffMember.Name), entity.Name);
        SetProperty(domain, nameof(StaffMember.Email), entity.Email);
        SetProperty(domain, nameof(StaffMember.IsActive), entity.IsActive);
        SetProperty(domain, nameof(StaffMember.IsDeleted), entity.IsDeleted);
        SetProperty(domain, nameof(StaffMember.CreatedAt), entity.CreatedAt);

        var windows = entity.AvailabilityWindows
            .Select(w => new StaffAvailabilityWindow(
                (DayOfWeek)w.DayOfWeek,
                w.StartTime,
                w.EndTime))
            .ToList();

        SetBackingField(domain, "_availabilityWindows", windows);

        return domain;
    }

    public static StaffMemberEntity ToEntity(StaffMember domain)
        => new()
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            TenantUserId = domain.TenantUserId,
            Name = domain.Name,
            Email = domain.Email,
            IsActive = domain.IsActive,
            IsDeleted = domain.IsDeleted,
            CreatedAt = domain.CreatedAt,
            AvailabilityWindows = domain.AvailabilityWindows
                .Select(w => new StaffAvailabilityWindowEntity
                {
                    Id = Guid.NewGuid(),
                    StaffMemberId = domain.Id,
                    DayOfWeek = (int)w.DayOfWeek,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime
                }).ToList()
        };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);

        prop?.SetValue(target, value);
    }

    private static void SetBackingField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        field?.SetValue(target, value);
    }
}
