namespace Chronith.Application.DTOs;

public sealed record ManualPaymentOptionsDto(
    string? QrCodeUrl,
    string? PublicNote,
    string Label
);
