using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Subscriptions;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetUsageQuery : IRequest<TenantUsageDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetUsageQueryHandler(
    ITenantSubscriptionRepository subRepo,
    ITenantPlanRepository planRepo,
    IBookingTypeRepository btRepo,
    IStaffMemberRepository staffRepo,
    IBookingRepository bookingRepo,
    ICustomerRepository customerRepo,
    ITenantContext tenantContext
) : IRequestHandler<GetUsageQuery, TenantUsageDto>
{
    public async Task<TenantUsageDto> Handle(
        GetUsageQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        var sub = await subRepo.GetActiveByTenantIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException("TenantSubscription", tenantId);

        var plan = await planRepo.GetByIdAsync(sub.PlanId, cancellationToken)
            ?? throw new NotFoundException("TenantPlan", sub.PlanId);

        var monthStart = new DateTimeOffset(
            DateTimeOffset.UtcNow.Year,
            DateTimeOffset.UtcNow.Month,
            1, 0, 0, 0, TimeSpan.Zero);

        // Run sequentially — EF Core DbContext is not thread-safe and cannot execute
        // multiple queries concurrently on the same scoped instance.
        var btCount = await btRepo.CountByTenantAsync(tenantId, cancellationToken);
        var staffCount = await staffRepo.CountByTenantAsync(tenantId, cancellationToken);
        var bookingCount = await bookingRepo.CountByTenantSinceAsync(tenantId, monthStart, cancellationToken);
        var customerCount = await customerRepo.CountByTenantAsync(tenantId, cancellationToken);

        return new TenantUsageDto(
            BookingTypesUsed: btCount,
            BookingTypesLimit: plan.MaxBookingTypes,
            StaffMembersUsed: staffCount,
            StaffMembersLimit: plan.MaxStaffMembers,
            BookingsThisMonth: bookingCount,
            BookingsPerMonthLimit: plan.MaxBookingsPerMonth,
            CustomersUsed: customerCount,
            CustomersLimit: plan.MaxCustomers,
            PlanName: plan.Name
        );
    }
}
