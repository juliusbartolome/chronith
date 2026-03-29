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

    /// <summary>
    /// Generates an HMAC-signed URL for staff verification of a booking payment.
    /// Uses a separate domain prefix from customer booking access for domain separation.
    /// </summary>
    string GenerateStaffVerifyUrl(string baseUrl, Guid bookingId, string tenantSlug);

    /// <summary>
    /// Validates an HMAC signature for staff verification. Returns false if expired or tampered.
    /// Uses a separate domain prefix — customer booking-access signatures are rejected.
    /// </summary>
    bool ValidateStaffVerify(Guid bookingId, string tenantSlug, long expires, string signature);
}
