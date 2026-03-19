namespace Chronith.Application.Services;

/// <summary>
/// Removes PII from audit snapshot JSON strings before they are persisted.
/// </summary>
public interface IAuditPiiRedactor
{
    /// <summary>
    /// Returns a copy of <paramref name="json"/> with known PII fields
    /// replaced by <c>"[REDACTED]"</c>. Returns <paramref name="json"/>
    /// unchanged if it is null, empty, or not valid JSON.
    /// </summary>
    string? Redact(string? json);
}
