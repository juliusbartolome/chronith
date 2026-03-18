namespace Chronith.Application.DTOs;

public sealed record PaymentProviderSummaryDto(
    Guid Id,
    string ProviderName,
    string Label,
    string? PublicNote,
    string? QrCodeUrl);
