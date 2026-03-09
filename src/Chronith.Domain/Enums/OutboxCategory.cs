using System.Text.Json.Serialization;

namespace Chronith.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutboxCategory
{
    TenantWebhook = 0,
    CustomerCallback = 1,
    Notification = 2
}
