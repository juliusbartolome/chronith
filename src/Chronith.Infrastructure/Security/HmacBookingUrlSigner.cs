using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Security;

public sealed class HmacBookingUrlSigner : IBookingUrlSigner
{
    private const string DomainPrefix = "booking-access";
    private const string StaffVerifyPrefix = "staff-verify";
    private readonly byte[] _key;
    private readonly int _lifetimeSeconds;
    private readonly int _staffVerifyLifetimeSeconds;

    public HmacBookingUrlSigner(
        IOptions<BlindIndexOptions> hmacOptions,
        IOptions<PaymentPageOptions> pageOptions)
    {
        var opts = hmacOptions.Value;
        if (string.IsNullOrWhiteSpace(opts.HmacKey))
            throw new InvalidOperationException(
                "Security:HmacKey must be set for booking URL signing.");

        try
        {
            _key = Convert.FromBase64String(opts.HmacKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Security:HmacKey is not valid Base64.", ex);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Security:HmacKey must be exactly 32 bytes. Got {_key.Length} bytes.");

        _lifetimeSeconds = pageOptions.Value.TokenLifetimeSeconds;
        _staffVerifyLifetimeSeconds = pageOptions.Value.StaffVerifyLifetimeSeconds;
    }

    public string GenerateSignedUrl(string baseUrl, Guid bookingId, string tenantSlug)
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(_lifetimeSeconds).ToUnixTimeSeconds();
        var signature = ComputeSignature(DomainPrefix, bookingId, tenantSlug, expires);

        return $"{baseUrl}?bookingId={bookingId}&tenantSlug={tenantSlug}&expires={expires}&sig={signature}";
    }

    public bool Validate(Guid bookingId, string tenantSlug, long expires, string signature)
    {
        if (expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return false;

        var expected = ComputeSignature(DomainPrefix, bookingId, tenantSlug, expires);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    public string GenerateStaffVerifyUrl(string baseUrl, Guid bookingId, string tenantSlug)
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(_staffVerifyLifetimeSeconds).ToUnixTimeSeconds();
        var signature = ComputeSignature(StaffVerifyPrefix, bookingId, tenantSlug, expires);

        return $"{baseUrl}?bookingId={bookingId}&tenantSlug={tenantSlug}&expires={expires}&sig={signature}";
    }

    public bool ValidateStaffVerify(Guid bookingId, string tenantSlug, long expires, string signature)
    {
        if (expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return false;

        var expected = ComputeSignature(StaffVerifyPrefix, bookingId, tenantSlug, expires);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private string ComputeSignature(string prefix, Guid bookingId, string tenantSlug, long expires)
    {
        var payload = $"{prefix}.{bookingId}.{tenantSlug}.{expires}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
