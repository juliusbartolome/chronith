using System.Text.Json.Serialization;

namespace Chronith.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecurrenceFrequency { Daily, Weekly, Monthly }
