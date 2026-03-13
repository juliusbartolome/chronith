namespace Chronith.Client.Models;

public sealed record TenantUsageDto(
    int BookingTypesCount,
    int StaffMembersCount,
    int BookingsThisMonth,
    int MaxBookingTypes,
    int MaxStaffMembers,
    int MaxBookingsPerMonth
);
