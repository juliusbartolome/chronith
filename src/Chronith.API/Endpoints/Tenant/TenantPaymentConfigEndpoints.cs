using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.DTOs;
using Chronith.Application.Queries.TenantPaymentConfig;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Tenant;

// --- Request models ---

public sealed class CreateTenantPaymentConfigRequest
{
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Settings { get; set; } = "{}";
    public string? PublicNote { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? PaymentSuccessUrl { get; set; }
    public string? PaymentFailureUrl { get; set; }
}

public sealed class UpdateTenantPaymentConfigRequest
{
    public string Label { get; set; } = string.Empty;
    public string Settings { get; set; } = "{}";
    public string? PublicNote { get; set; }
    public string? QrCodeUrl { get; set; }
    public string? PaymentSuccessUrl { get; set; }
    public string? PaymentFailureUrl { get; set; }
}

// --- GET /tenant/payment-config ---

public sealed class ListTenantPaymentConfigsEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<TenantPaymentConfigDto>>
{
    public override void Configure()
    {
        Get("/tenant/payment-config");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantRead}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new GetTenantPaymentConfigsQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}

// --- POST /tenant/payment-config ---

public sealed class CreateTenantPaymentConfigEndpoint(ISender sender)
    : Endpoint<CreateTenantPaymentConfigRequest, TenantPaymentConfigDto>
{
    public override void Configure()
    {
        Post("/tenant/payment-config");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantWrite}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CreateTenantPaymentConfigRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CreateTenantPaymentConfigCommand
        {
            ProviderName = req.ProviderName,
            Label = req.Label,
            Settings = req.Settings,
            PublicNote = req.PublicNote,
            QrCodeUrl = req.QrCodeUrl,
            PaymentSuccessUrl = req.PaymentSuccessUrl,
            PaymentFailureUrl = req.PaymentFailureUrl
        }, ct);

        await Send.OkAsync(result, ct);
    }
}

// --- PUT /tenant/payment-config/{id} ---

public sealed class UpdateTenantPaymentConfigEndpoint(ISender sender)
    : Endpoint<UpdateTenantPaymentConfigRequest, TenantPaymentConfigDto>
{
    public override void Configure()
    {
        Put("/tenant/payment-config/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantWrite}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(UpdateTenantPaymentConfigRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await sender.Send(new UpdateTenantPaymentConfigCommand
        {
            Id = id,
            Label = req.Label,
            Settings = req.Settings,
            PublicNote = req.PublicNote,
            QrCodeUrl = req.QrCodeUrl,
            PaymentSuccessUrl = req.PaymentSuccessUrl,
            PaymentFailureUrl = req.PaymentFailureUrl
        }, ct);

        await Send.OkAsync(result, ct);
    }
}

// --- DELETE /tenant/payment-config/{id} ---

public sealed class DeleteTenantPaymentConfigEndpoint(ISender sender)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/tenant/payment-config/{id}");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantWrite}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await sender.Send(new DeleteTenantPaymentConfigCommand(id), ct);
        await Send.OkAsync();
    }
}

// --- PATCH /tenant/payment-config/{id}/activate ---

public sealed class ActivateTenantPaymentConfigEndpoint(ISender sender)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/tenant/payment-config/{id}/activate");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantWrite}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await sender.Send(new ActivateTenantPaymentConfigCommand(id), ct);
        await Send.OkAsync();
    }
}

// --- PATCH /tenant/payment-config/{id}/deactivate ---

public sealed class DeactivateTenantPaymentConfigEndpoint(ISender sender)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Patch("/tenant/payment-config/{id}/deactivate");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantWrite}");
        Options(x => x.WithTags("Tenant").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        await sender.Send(new DeactivateTenantPaymentConfigCommand(id), ct);
        await Send.OkAsync();
    }
}
