namespace Chronith.Application.Services;

/// <summary>
/// Computes a deterministic HMAC-SHA256 token for equality-based lookup
/// of encrypted fields (blind index pattern).
/// </summary>
public interface IBlindIndexService
{
    /// <summary>
    /// Normalises <paramref name="value"/> to lowercase and returns its
    /// HMAC-SHA256 as a 64-character lowercase hex string.
    /// </summary>
    string ComputeToken(string value);
}
