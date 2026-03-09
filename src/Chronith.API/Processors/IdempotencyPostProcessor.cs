using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IdempotencyOptions = Chronith.Infrastructure.Services.IdempotencyOptions;

namespace Chronith.API.Processors;

public sealed class IdempotencyPostProcessor : IGlobalPostProcessor
{
    private const int MaxResponseBodyBytes = 65_536; // 64 KB

    public async Task PostProcessAsync(IPostProcessorContext ctx, CancellationToken ct)
    {
        var httpContext = ctx.HttpContext;

        // Always restore original body stream if one was captured, even on early-return paths (I1)
        if (!httpContext.Items.TryGetValue(IdempotencyPreProcessor.IdempotencyOriginalBodyKey, out var originalObj) ||
            originalObj is not Stream originalBodyStream)
        {
            // No capture stream was set up — nothing to restore
            return;
        }

        // From this point, we MUST restore the original stream and dispose the capture stream.
        var captureStream = httpContext.Response.Body;
        try
        {
            // If no idempotency data was stored by the pre-processor, just restore and exit
            if (!httpContext.Items.TryGetValue(IdempotencyPreProcessor.IdempotencyItemsKey, out var dataObj) ||
                dataObj is not IdempotencyData data)
                return;

            // Step 1: Read captured response from captureStream
            captureStream.Position = 0;
            var responseBody = await new StreamReader(captureStream, Encoding.UTF8, leaveOpen: true)
                .ReadToEndAsync(ct);

            var responseContentType = httpContext.Response.ContentType ?? "application/json";

            // Step 2: Save idempotency key to DB BEFORE flushing response to client (C2)
            if (Encoding.UTF8.GetByteCount(responseBody) <= MaxResponseBodyBytes)
            {
                var options = httpContext.RequestServices
                    .GetRequiredService<IOptions<IdempotencyOptions>>();
                var ttl = TimeSpan.FromHours(options.Value.ExpirationHours);

                var repo = httpContext.RequestServices.GetRequiredService<IIdempotencyKeyRepository>();
                var unitOfWork = httpContext.RequestServices.GetRequiredService<IUnitOfWork>();

                var idempotencyKey = IdempotencyKey.Create(
                    data.TenantId,
                    data.Key,
                    data.EndpointRoute,
                    data.RequestHash,
                    httpContext.Response.StatusCode,
                    responseBody,
                    responseContentType,
                    ttl);

                try
                {
                    await repo.AddAsync(idempotencyKey, ct);
                    await unitOfWork.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // C1: Concurrent request already saved this key — the unique constraint
                    // was violated. The client will still receive its response; the first-in
                    // record wins, so we just log and move on.
                    var logger = httpContext.RequestServices
                        .GetRequiredService<ILogger<IdempotencyPostProcessor>>();
                    logger.LogWarning(
                        "Idempotency key '{Key}' for route '{Route}' was already saved by a concurrent request",
                        data.Key, data.EndpointRoute);
                }
            }
            else
            {
                // I2: Response body exceeds 64 KB — skip saving the idempotency record
                var logger = httpContext.RequestServices
                    .GetRequiredService<ILogger<IdempotencyPostProcessor>>();
                logger.LogWarning(
                    "Response body for idempotency key '{Key}' on route '{Route}' exceeds {MaxBytes} bytes; skipping idempotency record",
                    data.Key, data.EndpointRoute, MaxResponseBodyBytes);
            }

            // Step 3: Copy captured response to original stream (flush to client)
            captureStream.Position = 0;
            await captureStream.CopyToAsync(originalBodyStream, ct);
        }
        finally
        {
            // I1: Always restore original body stream and dispose capture stream
            httpContext.Response.Body = originalBodyStream;
            await captureStream.DisposeAsync();
        }
    }
}
