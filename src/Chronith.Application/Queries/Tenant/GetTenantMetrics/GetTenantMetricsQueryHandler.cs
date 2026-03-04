using Chronith.Application.DTOs;
using Chronith.Application.Extensions;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.Tenant.GetTenantMetrics;

public sealed class GetTenantMetricsQueryHandler(
    ITenantContext tenantContext,
    IBookingRepository bookingRepository,
    IWebhookOutboxRepository outboxRepository,
    IBookingTypeRepository bookingTypeRepository,
    ITenantRepository tenantRepository)
    : IRequestHandler<GetTenantMetricsQuery, TenantMetricsDto>
{
    public async Task<TenantMetricsDto> Handle(
        GetTenantMetricsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId;

        // Resolve month start in tenant's timezone
        var tenant = await tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var now = DateTimeOffset.UtcNow;
        var tz = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZoneId);
        var tenantNow = TimeZoneInfo.ConvertTime(now, tz);
        var monthStart = new DateTimeOffset(
            tenantNow.Year, tenantNow.Month, 1, 0, 0, 0, tenantNow.Offset);
        var monthStartUtc = monthStart.ToUniversalTime();

        // Run aggregate queries sequentially — repositories share a single DbContext
        // which does not support concurrent operations on the same instance.
        var bookings = await bookingRepository.GetMetricsAsync(tenantId, monthStartUtc, cancellationToken);
        var deliveries = await outboxRepository.GetDeliveryMetricsAsync(tenantId, cancellationToken);
        var types = await bookingTypeRepository.GetTypeMetricsAsync(tenantId, cancellationToken);

        var totalDeliveries = deliveries.Delivered + deliveries.Failed;
        decimal? deliveryRatePct = totalDeliveries > 0
            ? Math.Round((decimal)deliveries.Delivered / totalDeliveries * 100, 1)
            : null;

        return new TenantMetricsDto(
            Bookings: new BookingMetricsDto(
                bookings.Total,
                bookings.ByStatus.ToDictionary(
                    kvp => kvp.Key.ToString().ToSnakeCase(),
                    kvp => kvp.Value),
                bookings.ThisMonth),
            Webhooks: new WebhookMetricsDto(
                totalDeliveries, deliveries.Delivered, deliveries.Failed, deliveryRatePct),
            BookingTypes: new BookingTypeMetricsDto(types.Active, types.Archived));
    }
}
