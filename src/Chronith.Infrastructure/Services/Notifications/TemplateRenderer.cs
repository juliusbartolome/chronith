using Chronith.Application.Interfaces;

namespace Chronith.Infrastructure.Services.Notifications;

public sealed class TemplateRenderer : ITemplateRenderer
{
    public string Render(string template, IReadOnlyDictionary<string, string> context)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var result = template;
        foreach (var (key, value) in context)
        {
            result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }
}
