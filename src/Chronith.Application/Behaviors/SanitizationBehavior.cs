using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MediatR;

namespace Chronith.Application.Behaviors;

/// <summary>
/// Strips HTML tags from string properties of MediatR requests.
/// NOTE: Properties with init-only setters (records with positional or required init properties)
/// are skipped, as they cannot be mutated after construction. This behavior is effective for
/// future commands using mutable properties or for commands that explicitly opt in by
/// declaring settable string properties.
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
            .Where(p => p.PropertyType == typeof(string) && p.CanWrite && !IsInitOnly(p));

        foreach (var property in properties)
        {
            var value = property.GetValue(request) as string;
            if (value is null)
                continue;

            var sanitized = DangerousBlockRegex().Replace(value, string.Empty);
            sanitized = HtmlTagRegex().Replace(sanitized, string.Empty);
            property.SetValue(request, sanitized);
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
