namespace Chronith.Domain.Models;

using System.Text.Json;
using Chronith.Domain.Exceptions;

public sealed record CustomFieldDefinition(string Name, string Type, bool Required);

public static class CustomFieldValidator
{
    private static readonly HashSet<string> ValidTypes = ["text", "number", "boolean", "date"];

    public static void Validate(
        string? customFieldsJson,
        IReadOnlyList<CustomFieldDefinition> schema)
    {
        if (schema.Count == 0) return;
        if (string.IsNullOrWhiteSpace(customFieldsJson))
        {
            if (schema.Any(f => f.Required))
                throw new CustomFieldValidationException("Missing required custom fields.");
            return;
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(customFieldsJson)
            ?? throw new CustomFieldValidationException("Invalid custom fields JSON.");

        var schemaMap = schema.ToDictionary(f => f.Name);

        // Check for unknown fields
        var unknownField = data.Keys.FirstOrDefault(key => !schemaMap.ContainsKey(key));
        if (unknownField is not null)
            throw new CustomFieldValidationException($"Unknown custom field: {unknownField}");

        // Check required + type
        foreach (var field in schema)
        {
            if (!data.TryGetValue(field.Name, out var value))
            {
                if (field.Required)
                    throw new CustomFieldValidationException($"Required custom field missing: {field.Name}");
                continue;
            }
            ValidateType(field, value);
        }
    }

    private static void ValidateType(CustomFieldDefinition field, JsonElement value)
    {
        var valid = field.Type switch
        {
            "text" => value.ValueKind == JsonValueKind.String,
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "date" => value.ValueKind == JsonValueKind.String &&
                      DateTimeOffset.TryParse(value.GetString(), out _),
            _ => false
        };
        if (!valid)
            throw new CustomFieldValidationException($"Custom field '{field.Name}' must be of type '{field.Type}'.");
    }
}
