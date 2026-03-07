namespace Chronith.Infrastructure.Services;

public sealed class WaitlistPromotionOptions
{
    public int CheckIntervalSeconds { get; set; } = 30;
    public int OfferTtlMinutes { get; set; } = 60;
}
