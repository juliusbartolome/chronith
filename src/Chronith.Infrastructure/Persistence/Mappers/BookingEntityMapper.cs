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
        SetPrivate(domain, nameof(Booking.FirstName), entity.FirstName);
        SetPrivate(domain, nameof(Booking.LastName), entity.LastName);
        SetPrivate(domain, nameof(Booking.Mobile), entity.Mobile);
        SetPrivate(domain, nameof(Booking.CustomerAccountId), entity.CustomerAccountId);
        SetPrivate(domain, nameof(Booking.PaymentReference), entity.PaymentReference);
        SetPrivate(domain, nameof(Booking.AmountInCentavos), entity.AmountInCentavos);
        SetPrivate(domain, nameof(Booking.Currency), entity.Currency);
        SetPrivate(domain, nameof(Booking.CheckoutUrl), entity.CheckoutUrl);
        SetPrivate(domain, nameof(Booking.ProofOfPaymentUrl), entity.ProofOfPaymentUrl);
        SetPrivate(domain, nameof(Booking.ProofOfPaymentFileName), entity.ProofOfPaymentFileName);
        SetPrivate(domain, nameof(Booking.PaymentNote), entity.PaymentNote);
        SetPrivate(domain, nameof(Booking.StaffMemberId), entity.StaffMemberId);
        SetPrivate(domain, nameof(Booking.CustomFields), entity.CustomFields);
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
            FirstName = domain.FirstName,
            LastName = domain.LastName,
            Mobile = domain.Mobile,
            CustomerAccountId = domain.CustomerAccountId,
            PaymentReference = domain.PaymentReference,
            AmountInCentavos = domain.AmountInCentavos,
            Currency = domain.Currency,
            CheckoutUrl = domain.CheckoutUrl,
            ProofOfPaymentUrl = domain.ProofOfPaymentUrl,
            ProofOfPaymentFileName = domain.ProofOfPaymentFileName,
            PaymentNote = domain.PaymentNote,
            StaffMemberId = domain.StaffMemberId,
            CustomFields = domain.CustomFields,
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
