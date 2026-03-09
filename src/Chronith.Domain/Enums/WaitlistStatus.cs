using System.Text.Json.Serialization;

namespace Chronith.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WaitlistStatus { Waiting, Offered, Expired, Converted }
