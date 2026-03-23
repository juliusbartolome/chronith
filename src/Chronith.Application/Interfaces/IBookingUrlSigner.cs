namespace Chronith.Application.Interfaces;

/// <summary>
/// Signs and validates HMAC-authenticated URLs for booking access.
/// </summary>
public interface IBookingUrlSigner
{
    /// <summary>
    /// Generates an HMAC-signed URL by appending bookingId, tenantSlug, expires, and sig query params.
    /// </summary>
    string GenerateSignedUrl(string baseUrl, Guid bookingId, string tenantSlug);

    /// <summary>
    /// Validates an HMAC signature for booking access. Returns false if expired or tampered.
    /// </summary>
    bool Validate(Guid bookingId, string tenantSlug, long expires, string signature);
}
