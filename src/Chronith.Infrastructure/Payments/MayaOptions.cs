namespace Chronith.Infrastructure.Payments;

public sealed class MayaOptions
{
    public string PublicApiKey { get; set; } = string.Empty;
    public string SecretApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://pg-sandbox.paymaya.com";
    public string[] AllowedWebhookIps { get; set; } = [];
    public string SuccessUrl { get; set; } = string.Empty;
    public string FailureUrl { get; set; } = string.Empty;
}
