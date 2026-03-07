using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

/// <summary>
/// Maps between BookingEntity and Booking domain object.
/// Static only — no reflection mappers.
/// </summary>
public static class BookingEntityMapper
{
    public static Booking ToDomain(BookingEntity entity)
    {
        var domain = new Booking();

        SetPrivate(domain, nameof(Booking.Id), entity.Id);
        SetPrivate(domain, nameof(Booking.TenantId), entity.TenantId);
        SetPrivate(domain, nameof(Booking.BookingTypeId), entity.BookingTypeId);
        SetPrivate(domain, nameof(Booking.Start), entity.Start);
        SetPrivate(domain, nameof(Booking.End), entity.End);
        SetPrivate(domain, nameof(Booking.Status), entity.Status);
        SetPrivate(domain, nameof(Booking.CustomerId), entity.CustomerId);
        SetPrivate(domain, nameof(Booking.CustomerEmail), entity.CustomerEmail);
        SetPrivate(domain, nameof(Booking.PaymentReference), entity.PaymentReference);
        SetPrivate(domain, nameof(Booking.AmountInCentavos), entity.AmountInCentavos);
        SetPrivate(domain, nameof(Booking.Currency), entity.Currency);
        SetPrivate(domain, nameof(Booking.CheckoutUrl), entity.CheckoutUrl);
        SetPrivate(domain, nameof(Booking.StaffMemberId), entity.StaffMemberId);
        SetPrivate(domain, nameof(Booking.IsDeleted), entity.IsDeleted);
        SetPrivate(domain, nameof(Booking.RowVersion), entity.RowVersion);

        return domain;
    }

    public static BookingEntity ToEntity(Booking domain)
        => new BookingEntity
        {
            Id = domain.Id,
            TenantId = domain.TenantId,
            BookingTypeId = domain.BookingTypeId,
            Start = domain.Start,
            End = domain.End,
            Status = domain.Status,
            CustomerId = domain.CustomerId,
            CustomerEmail = domain.CustomerEmail,
            PaymentReference = domain.PaymentReference,
            AmountInCentavos = domain.AmountInCentavos,
            Currency = domain.Currency,
            CheckoutUrl = domain.CheckoutUrl,
            StaffMemberId = domain.StaffMemberId,
            IsDeleted = domain.IsDeleted,
            RowVersion = domain.RowVersion,
            StatusChanges = domain.StatusChanges.Select(sc => new BookingStatusChangeEntity
            {
                Id = sc.Id,
                BookingId = sc.BookingId,
                FromStatus = sc.FromStatus,
                ToStatus = sc.ToStatus,
                ChangedById = sc.ChangedById,
                ChangedByRole = sc.ChangedByRole,
                ChangedAt = sc.ChangedAt
            }).ToList()
        };

    private static void SetPrivate(object target, string propertyName, object? value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        prop?.SetValue(target, value);
    }
}
