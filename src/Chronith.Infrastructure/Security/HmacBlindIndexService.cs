using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Services;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Security;

public sealed class HmacBlindIndexService : IBlindIndexService
{
    private readonly byte[] _key;

    public HmacBlindIndexService(IOptions<BlindIndexOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.HmacKey))
            throw new InvalidOperationException(
                "BlindIndexOptions.HmacKey must be set. " +
                "Set Security:HmacKey to a Base64-encoded 32-byte key.");

        try
        {
            _key = Convert.FromBase64String(opts.HmacKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "BlindIndexOptions.HmacKey is not valid Base64. " +
                "Security:HmacKey must be a Base64-encoded 32-byte key.", ex);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Security:HmacKey must be exactly 32 bytes (256-bit). Got {_key.Length} bytes.");
    }

    public string ComputeToken(string value)
    {
        var normalised = value.ToLowerInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalised);
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
