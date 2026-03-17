using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class WaitlistEntryMapper
{
    public static WaitlistEntryDto ToDto(this WaitlistEntry entry) =>
        new(
            Id: entry.Id,
            BookingTypeId: entry.BookingTypeId,
            StaffMemberId: entry.StaffMemberId,
            CustomerId: entry.CustomerId,
            CustomerEmail: entry.CustomerEmail,
            DesiredStart: entry.DesiredStart,
            DesiredEnd: entry.DesiredEnd,
            Status: entry.Status,
            OfferedAt: entry.OfferedAt,
            ExpiresAt: entry.ExpiresAt,
            CreatedAt: entry.CreatedAt);
}
