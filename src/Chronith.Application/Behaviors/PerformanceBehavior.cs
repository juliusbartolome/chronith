using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Chronith.Application.Behaviors;

/// <summary>
/// Logs a warning when a handler exceeds its performance threshold.
/// Read threshold: 50ms, Write threshold: 75ms.
/// Distinguishes reads by checking if TRequest implements IQuery marker.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly bool IsQuery = typeof(TRequest).GetInterface(nameof(IQuery)) is not null;
    private static readonly int ThresholdMs = IsQuery ? 50 : 75;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();

        if (sw.ElapsedMilliseconds > ThresholdMs)
        {
            logger.LogWarning(
                "Slow {RequestType}: {Elapsed}ms exceeded {Threshold}ms threshold",
                typeof(TRequest).Name,
                sw.ElapsedMilliseconds,
                ThresholdMs);
        }

        return response;
    }
}

/// <summary>Marker interface for query requests (used by PerformanceBehavior).</summary>
public interface IQuery { }
