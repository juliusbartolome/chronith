namespace Chronith.Domain.Enums;

public enum OutboxStatus
{
    Pending,
    Delivered,
    Failed,
    Abandoned   // CustomerCallback URL was removed; entry will not be retried
}
