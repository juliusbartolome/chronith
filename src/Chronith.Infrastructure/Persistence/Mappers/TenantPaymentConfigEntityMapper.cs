using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantPaymentConfigEntityMapper
{
    public static TenantPaymentConfig ToDomain(TenantPaymentConfigEntity entity)
    {
        var domain = new TenantPaymentConfig();
        SetProperty(domain, nameof(TenantPaymentConfig.Id), entity.Id);
        SetProperty(domain, nameof(TenantPaymentConfig.TenantId), entity.TenantId);
        SetProperty(domain, nameof(TenantPaymentConfig.ProviderName), entity.ProviderName);
        SetProperty(domain, nameof(TenantPaymentConfig.Label), entity.Label);
        SetProperty(domain, nameof(TenantPaymentConfig.IsActive), entity.IsActive);
        SetProperty(domain, nameof(TenantPaymentConfig.IsDeleted), entity.IsDeleted);
        SetProperty(domain, nameof(TenantPaymentConfig.Settings), entity.Settings);
        SetProperty(domain, nameof(TenantPaymentConfig.PublicNote), entity.PublicNote);
        SetProperty(domain, nameof(TenantPaymentConfig.QrCodeUrl), entity.QrCodeUrl);
        SetProperty(domain, nameof(TenantPaymentConfig.PaymentSuccessUrl), entity.PaymentSuccessUrl);
        SetProperty(domain, nameof(TenantPaymentConfig.PaymentFailureUrl), entity.PaymentFailureUrl);
        SetProperty(domain, nameof(TenantPaymentConfig.CreatedAt), entity.CreatedAt);
        SetProperty(domain, nameof(TenantPaymentConfig.UpdatedAt), entity.UpdatedAt);
        return domain;
    }

    public static TenantPaymentConfigEntity ToEntity(TenantPaymentConfig domain) => new()
    {
        Id = domain.Id,
        TenantId = domain.TenantId,
        ProviderName = domain.ProviderName,
        Label = domain.Label,
        IsActive = domain.IsActive,
        IsDeleted = domain.IsDeleted,
        Settings = domain.Settings,
        PublicNote = domain.PublicNote,
        QrCodeUrl = domain.QrCodeUrl,
        PaymentSuccessUrl = domain.PaymentSuccessUrl,
        PaymentFailureUrl = domain.PaymentFailureUrl,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt
    };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        prop?.SetValue(target, value);
    }
}
