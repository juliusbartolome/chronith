using System.Text.RegularExpressions;

namespace Chronith.Application.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Converts a PascalCase or camelCase string to snake_case.
    /// E.g. "PendingPayment" → "pending_payment".
    /// </summary>
    public static string ToSnakeCase(this string s) =>
        Regex.Replace(s, "(?<=[a-z0-9])([A-Z])", "_$1").ToLower();
}
