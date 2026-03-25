namespace Chronith.Application.DTOs;

public sealed record TenantPaymentConfigDto(
    Guid Id,
    Guid TenantId,
    string ProviderName,
    string Label,
    bool IsActive,
    string? PublicNote,
    string? QrCodeUrl,
    string? PaymentSuccessUrl,
    string? PaymentFailureUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
