namespace Chronith.Application.Options;

public sealed class PaymentPageOptions
{
    public const string SectionName = "PaymentPage";

    /// <summary>
    /// Base URL for the payment selection page (e.g. "https://booking.example.com/pay").
    /// HMAC query parameters are appended by IBookingUrlSigner.
    /// </summary>
    public string BaseUrl { get; set; } = "https://example.com/pay";

    /// <summary>
    /// Lifetime of generated booking access tokens in seconds. Default: 3600 (1 hour).
    /// </summary>
    public int TokenLifetimeSeconds { get; set; } = 3600;
}
