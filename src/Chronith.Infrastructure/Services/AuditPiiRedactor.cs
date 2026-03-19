using System.Text.Json;
using System.Text.Json.Nodes;
using Chronith.Application.Services;

namespace Chronith.Infrastructure.Services;

public sealed class AuditPiiRedactor : IAuditPiiRedactor
{
    private static readonly HashSet<string> PiiKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "CustomerEmail", "Phone", "PasswordHash"
    };

    public string? Redact(string? json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj) return json;

            foreach (var key in PiiKeys)
            {
                if (obj.ContainsKey(key))
                    obj[key] = "[REDACTED]";
            }

            return obj.ToJsonString();
        }
        catch (JsonException)
        {
            return json; // not valid JSON — return unchanged
        }
    }
}
