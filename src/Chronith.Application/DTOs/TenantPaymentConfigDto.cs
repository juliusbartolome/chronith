namespace Chronith.Application.DTOs;

public sealed record TenantPaymentConfigDto(
    Guid Id,
    Guid TenantId,
    string ProviderName,
    string Label,
    bool IsActive,
    string Settings,
    string? PublicNote,
    string? QrCodeUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
