namespace Chronith.Domain.Exceptions;

public sealed class PlanLimitExceededException : DomainException
{
    public string ResourceType { get; }
    public int CurrentCount { get; }
    public int Limit { get; }

    public PlanLimitExceededException(string resourceType, int currentCount, int limit)
        : base($"Plan limit exceeded for {resourceType}: {currentCount}/{limit}. Upgrade your plan to add more.")
    {
        ResourceType = resourceType;
        CurrentCount = currentCount;
        Limit = limit;
    }
}
