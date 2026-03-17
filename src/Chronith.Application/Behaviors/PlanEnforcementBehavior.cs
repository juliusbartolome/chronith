using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Chronith.Application.Behaviors;

/// <summary>
/// Marker interface for commands that require plan-limit enforcement.
/// Implement on CreateBookingTypeCommand, CreateStaffCommand, CreateBookingCommand, etc.
/// </summary>
public interface IPlanEnforcedCommand
{
    /// <summary>The resource type being created, e.g. "BookingType", "StaffMember", "Booking".</summary>
    string EnforcedResourceType { get; }
}

/// <summary>
/// MediatR pipeline behavior that enforces subscription plan limits.
/// Only runs when TRequest implements <see cref="IPlanEnforcedCommand"/>.
/// Passes through immediately for commands that don't require enforcement.
/// </summary>
public sealed class PlanEnforcementBehavior<TRequest, TResponse>(
    ITenantSubscriptionRepository subRepo,
    ITenantPlanRepository planRepo,
    IBookingTypeRepository btRepo,
    IStaffMemberRepository staffRepo,
    IBookingRepository bookingRepo,
    ICustomerRepository customerRepo,
    ITenantContext tenantContext,
    ILogger<PlanEnforcementBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IPlanEnforcedCommand enforced)
            return await next(cancellationToken);

        var tenantId = tenantContext.TenantId;

        var sub = await subRepo.GetActiveByTenantIdAsync(tenantId, cancellationToken);
        if (sub is null)
        {
            logger.LogWarning(
                "PlanEnforcementBehavior: no active subscription for tenant {TenantId}, allowing request.",
                tenantId);
            return await next(cancellationToken);
        }

        var plan = await planRepo.GetByIdAsync(sub.PlanId, cancellationToken);
        if (plan is null)
        {
            logger.LogWarning(
                "PlanEnforcementBehavior: plan {PlanId} not found for tenant {TenantId}, allowing request.",
                sub.PlanId, tenantId);
            return await next(cancellationToken);
        }

        await EnforceAsync(enforced.EnforcedResourceType, tenantId, plan, cancellationToken);

        return await next(cancellationToken);
    }

    private async Task EnforceAsync(
        string resourceType,
        Guid tenantId,
        Domain.Models.TenantPlan plan,
        CancellationToken ct)
    {
        switch (resourceType)
        {
            case "BookingType":
            {
                var count = await btRepo.CountByTenantAsync(tenantId, ct);
                if (count >= plan.MaxBookingTypes)
                    throw new PlanLimitExceededException(resourceType, count, plan.MaxBookingTypes);
                break;
            }
            case "StaffMember":
            {
                var count = await staffRepo.CountByTenantAsync(tenantId, ct);
                if (count >= plan.MaxStaffMembers)
                    throw new PlanLimitExceededException(resourceType, count, plan.MaxStaffMembers);
                break;
            }
            case "Booking":
            {
                var monthStart = new DateTimeOffset(
                    DateTimeOffset.UtcNow.Year,
                    DateTimeOffset.UtcNow.Month,
                    1, 0, 0, 0, TimeSpan.Zero);
                var count = await bookingRepo.CountByTenantSinceAsync(tenantId, monthStart, ct);
                if (count >= plan.MaxBookingsPerMonth)
                    throw new PlanLimitExceededException(resourceType, count, plan.MaxBookingsPerMonth);
                break;
            }
            case "Customer":
            {
                var count = await customerRepo.CountByTenantAsync(tenantId, ct);
                if (count >= plan.MaxCustomers)
                    throw new PlanLimitExceededException(resourceType, count, plan.MaxCustomers);
                break;
            }
            default:
                logger.LogWarning(
                    "PlanEnforcementBehavior: unknown resource type '{ResourceType}', skipping limit check.",
                    resourceType);
                break;
        }
    }
}
