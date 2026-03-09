namespace Chronith.Infrastructure.Services;

public sealed class RecurringBookingGeneratorOptions
{
    public int GenerationHorizonDays { get; set; } = 30;
    public int CheckIntervalHours { get; set; } = 24;
}
