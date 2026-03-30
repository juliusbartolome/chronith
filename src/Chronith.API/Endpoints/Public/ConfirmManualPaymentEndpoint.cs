using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Chronith.API.Endpoints.Public;

public sealed class ConfirmManualPaymentRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public Guid BookingId { get; set; }

    [QueryParam]
    public long Expires { get; set; }

    [QueryParam]
    public string Sig { get; set; } = string.Empty;

    public IFormFile? ProofFile { get; set; }
    public string? PaymentNote { get; set; }
}

public sealed class ConfirmManualPaymentEndpoint(ISender sender)
    : Endpoint<ConfirmManualPaymentRequest, PublicBookingStatusDto>
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    public override void Configure()
    {
        Post("/public/{tenantSlug}/bookings/{bookingId}/confirm-payment");
        AllowAnonymous();
        AllowFileUploads();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(ConfirmManualPaymentRequest req, CancellationToken ct)
    {
        if (req.ProofFile is not null && req.ProofFile.Length > MaxFileSize)
        {
            AddError("ProofFile", "File size must not exceed 5 MB.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        Stream? proofStream = null;
        string? proofFileName = null;
        string? proofContentType = null;

        if (req.ProofFile is not null)
        {
            proofStream = req.ProofFile.OpenReadStream();
            proofFileName = req.ProofFile.FileName;
            proofContentType = req.ProofFile.ContentType;
        }

        try
        {
            var command = new ConfirmManualPaymentCommand
            {
                TenantSlug = req.TenantSlug,
                BookingId = req.BookingId,
                Expires = req.Expires,
                Signature = req.Sig,
                ProofFile = proofStream,
                ProofFileName = proofFileName,
                ProofContentType = proofContentType,
                PaymentNote = req.PaymentNote
            };

            var result = await sender.Send(command, ct);
            await Send.OkAsync(result, ct);
        }
        finally
        {
            if (proofStream is not null)
                await proofStream.DisposeAsync();
        }
    }
}
