using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FastEndpoints;

namespace Chronith.API.Processors;

public sealed class IdempotencyPostProcessor : IGlobalPostProcessor
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public async Task PostProcessAsync(IPostProcessorContext ctx, CancellationToken ct)
    {
        var httpContext = ctx.HttpContext;

        // Check if pre-processor stored idempotency data
        if (!httpContext.Items.TryGetValue(IdempotencyPreProcessor.IdempotencyItemsKey, out var dataObj) ||
            dataObj is not IdempotencyData data)
            return;

        // Restore original body stream and read captured response
        if (!httpContext.Items.TryGetValue(IdempotencyPreProcessor.IdempotencyOriginalBodyKey, out var originalObj) ||
            originalObj is not Stream originalBodyStream)
            return;

        var captureStream = httpContext.Response.Body;
        captureStream.Position = 0;
        var responseBody = await new StreamReader(captureStream, Encoding.UTF8).ReadToEndAsync(ct);

        // Copy captured response to original stream
        captureStream.Position = 0;
        await captureStream.CopyToAsync(originalBodyStream, ct);
        httpContext.Response.Body = originalBodyStream;

        // Save idempotency key to repository
        var repo = httpContext.RequestServices.GetRequiredService<IIdempotencyKeyRepository>();
        var unitOfWork = httpContext.RequestServices.GetRequiredService<IUnitOfWork>();

        var idempotencyKey = IdempotencyKey.Create(
            data.TenantId,
            data.Key,
            data.EndpointRoute,
            data.RequestHash,
            httpContext.Response.StatusCode,
            responseBody,
            DefaultTtl);

        await repo.AddAsync(idempotencyKey, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
