using System.Text.Json.Serialization;

namespace Chronith.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutboxStatus
{
    Pending,
    Delivered,
    Failed,
    Abandoned   // CustomerCallback URL was removed; entry will not be retried
}
