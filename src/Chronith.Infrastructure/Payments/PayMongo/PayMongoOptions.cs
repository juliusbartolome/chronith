namespace Chronith.Infrastructure.Payments.PayMongo;

public sealed class PayMongoOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paymongo.com/v1";
    public int WebhookTimestampToleranceSeconds { get; set; } = 300;
    public string SuccessUrl { get; set; } = string.Empty;
    public string FailureUrl { get; set; } = string.Empty;
}
