using System.Text.Json.Serialization;

namespace Chronith.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TenantUserRole
{
    Owner,
    Admin,
    Member
}
