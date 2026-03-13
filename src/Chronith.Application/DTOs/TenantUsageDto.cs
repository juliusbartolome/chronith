namespace Chronith.Application.DTOs;

public sealed record TenantUsageDto(
    int BookingTypesUsed,
    int BookingTypesLimit,
    int StaffMembersUsed,
    int StaffMembersLimit,
    int BookingsThisMonth,
    int BookingsPerMonthLimit,
    int CustomersUsed,
    int CustomersLimit,
    string PlanName
);
