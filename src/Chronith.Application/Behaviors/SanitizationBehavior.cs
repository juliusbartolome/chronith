using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using MediatR;

namespace Chronith.Application.Behaviors;

public sealed partial class SanitizationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Strip dangerous block-level script/style elements including their content
    [GeneratedRegex("<(script|style)[^>]*>[\\s\\S]*?<\\/\\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousBlockRegex();

    // Strip remaining HTML tags (leaves inner text intact)
    [GeneratedRegex("<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var properties = typeof(TRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (property.PropertyType != typeof(string))
                continue;

            if (!property.CanWrite)
                continue;

            // Skip init-only properties — their setter is decorated with IsExternalInit
            if (IsInitOnly(property))
                continue;

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
