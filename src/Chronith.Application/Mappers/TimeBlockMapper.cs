using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TimeBlockMapper
{
    public static TimeBlockDto ToDto(this TimeBlock block) =>
        new(
            Id: block.Id,
            BookingTypeId: block.BookingTypeId,
            StaffMemberId: block.StaffMemberId,
            Start: block.Start,
            End: block.End,
            Reason: block.Reason,
            CreatedAt: block.CreatedAt);
}
