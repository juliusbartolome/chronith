using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantPaymentConfigMapper
{
    public static TenantPaymentConfigDto ToDto(this TenantPaymentConfig config) => new(
        config.Id,
        config.TenantId,
        config.ProviderName,
        config.Label,
        config.IsActive,
        config.Settings,
        config.PublicNote,
        config.QrCodeUrl,
        config.CreatedAt,
        config.UpdatedAt);

    public static PaymentProviderSummaryDto ToSummaryDto(this TenantPaymentConfig config) => new(
        config.Id,
        config.ProviderName,
        config.Label,
        config.PublicNote,
        config.QrCodeUrl);
}
