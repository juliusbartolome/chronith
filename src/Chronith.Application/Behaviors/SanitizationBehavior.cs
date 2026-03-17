using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MediatR;

namespace Chronith.Application.Behaviors;

/// <summary>
/// Strips HTML tags from string properties of MediatR requests.
/// Supports both mutable properties (set via property setter) and init-only properties
/// (set via compiler-generated backing field reflection, the same technique used in
/// infrastructure entity mappers).
/// </summary>
public sealed partial class SanitizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Strip dangerous block-level script/style elements including their content
    [GeneratedRegex("<(script|style)[^>]*>[\\s\\S]*?<\\/\\1>", RegexOptions.IgnoreCase)]
    private static partial Regex DangerousBlockRegex();

    // Strip remaining HTML tags (leaves inner text intact)
    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagRegex();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var properties = typeof(TRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && (p.CanWrite || IsInitOnly(p)));

        foreach (var property in properties)
        {
            var value = property.GetValue(request) as string;
            if (value is null)
                continue;

            var sanitized = DangerousBlockRegex().Replace(value, string.Empty);
            sanitized = HtmlTagRegex().Replace(sanitized, string.Empty);

            if (!IsInitOnly(property))
            {
                property.SetValue(request, sanitized);
            }
            else
            {
                // For init-only properties (record types with required init),
                // the CLR blocks SetValue through the property accessor.
                // Set the compiler-generated backing field directly instead.
                var backingField = typeof(TRequest).GetField(
                    $"<{property.Name}>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                backingField?.SetValue(request, sanitized);
            }
        }

        return await next(cancellationToken);
    }

    private static bool IsInitOnly(PropertyInfo property)
    {
        var setMethod = property.SetMethod;
        if (setMethod is null)
            return false;

        return setMethod.ReturnParameter
            .GetRequiredCustomModifiers()
            .Contains(typeof(IsExternalInit));
    }
}
