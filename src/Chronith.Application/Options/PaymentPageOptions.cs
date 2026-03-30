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

    /// <summary>
    /// Lifetime of generated staff verification tokens in seconds. Default: 86400 (24 hours).
    /// </summary>
    public int StaffVerifyLifetimeSeconds { get; set; } = 86400;

    /// <summary>
    /// Base URL for the staff payment-verification page (e.g. "https://booking.example.com/verify").
    /// HMAC query parameters are appended by IBookingUrlSigner.
    /// </summary>
    public string StaffVerifyBaseUrl { get; set; } = "https://example.com/verify";
}
