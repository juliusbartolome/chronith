namespace Chronith.Client.Models;

/// <summary>Generic paginated response wrapper.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
