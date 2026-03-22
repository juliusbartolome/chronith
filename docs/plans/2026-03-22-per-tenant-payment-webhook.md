# Per-Tenant Payment Webhook URL Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Change the payment webhook endpoint from a single global route `/webhooks/payments/{provider}` to a per-tenant route `/webhooks/payments/{tenantId}/{provider}`, so that each tenant registers their own webhook URL in PayMongo and the webhook secret is validated per-tenant from the DB (not from a global app setting).

**Architecture:** The webhook endpoint extracts `tenantId` from the route and passes it to `ProcessPaymentWebhookCommand`. The handler swaps out the singleton `IPaymentProviderFactory` for the per-tenant `ITenantPaymentProviderResolver`, which loads the active payment config for that tenant from the DB and builds a `PayMongoProvider` with the tenant's credentials (including `webhookSecret`). `TenantPaymentConfigs` has no global EF query filter, so no `IgnoreQueryFilters()` is needed.

**Tech Stack:** FastEndpoints, MediatR, EF Core (Npgsql), xUnit, FluentAssertions, NSubstitute

---

## Task 1: Update `ProcessPaymentWebhookCommand` — add `TenantId`, swap to resolver

**Files:**

- Modify: `src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs`

**Context:**  
Currently the handler injects `IPaymentProviderFactory` (a singleton that reads `WebhookSecret` from `appsettings.json`) and resolves the provider by name only. We need it to resolve per-tenant using `ITenantPaymentProviderResolver`, which loads the active config from the DB for `(tenantId, providerName)` and builds a `PayMongoProvider` with the tenant's `Settings` JSON (including `webhookSecret`).

If the resolver returns `null` (no active config found for that tenant+provider), treat it the same as a failed signature check — throw `UnauthorizedException("Webhook validation failed")`. This avoids leaking tenant config existence to callers.

**Step 1: Write the failing unit test**

Create `tests/Chronith.Tests.Unit/Application/ProcessPaymentWebhookHandlerTests.cs`:

```csharp
using Chronith.Application.Commands.Bookings;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using MediatR;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Chronith.Tests.Unit.Application;

public sealed class ProcessPaymentWebhookHandlerTests
{
    private readonly ITenantPaymentProviderResolver _resolver;
    private readonly IBookingRepository _bookingRepo;
    private readonly IBookingTypeRepository _bookingTypeRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ProcessPaymentWebhookHandler _handler;

    private static readonly Guid TenantId = Guid.NewGuid();

    public ProcessPaymentWebhookHandlerTests()
    {
        _resolver = Substitute.For<ITenantPaymentProviderResolver>();
        _bookingRepo = Substitute.For<IBookingRepository>();
        _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _publisher = Substitute.For<IPublisher>();
        _handler = new ProcessPaymentWebhookHandler(
            _resolver, _bookingRepo, _bookingTypeRepo, _unitOfWork, _publisher);
    }

    [Fact]
    public async Task Handle_WhenResolverReturnsNull_ThrowsUnauthorizedException()
    {
        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ThrowsUnauthorizedException()
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(false);

        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(provider);

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_WhenEventIsNotSuccess_DoesNotUpdateBooking()
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
        provider.ParseWebhookPayload(Arg.Any<string>())
            .Returns(new WebhookPaymentEvent("ref-123", PaymentEventType.Failed));

        _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(provider);

        var cmd = new ProcessPaymentWebhookCommand
        {
            TenantId = TenantId,
            ProviderName = "PayMongo",
            RawBody = "{}",
            Headers = new Dictionary<string, string>()
        };

        await _handler.Handle(cmd, CancellationToken.None);

        await _bookingRepo.DidNotReceive().UpdateAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ProcessPaymentWebhookHandlerTests" -v
```

Expected: FAIL — `ProcessPaymentWebhookHandler` constructor does not accept `ITenantPaymentProviderResolver`

**Step 3: Update the command and handler**

Replace the contents of `src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs`:

```csharp
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Bookings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record ProcessPaymentWebhookCommand : IRequest
{
    public required Guid TenantId { get; init; }
    public required string ProviderName { get; init; }
    public required string RawBody { get; init; }
    public required IDictionary<string, string> Headers { get; init; }
    public string? SourceIpAddress { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class ProcessPaymentWebhookValidator
    : AbstractValidator<ProcessPaymentWebhookCommand>
{
    public ProcessPaymentWebhookValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ProviderName).NotEmpty();
        RuleFor(x => x.RawBody).NotEmpty();
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class ProcessPaymentWebhookHandler(
    ITenantPaymentProviderResolver resolver,
    IBookingRepository bookingRepo,
    IBookingTypeRepository bookingTypeRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher)
    : IRequestHandler<ProcessPaymentWebhookCommand>
{
    public async Task Handle(ProcessPaymentWebhookCommand cmd, CancellationToken ct)
    {
        // Resolve the provider per-tenant — loads webhook secret from the tenant's payment config
        var provider = await resolver.ResolveAsync(cmd.TenantId, cmd.ProviderName, ct);
        if (provider is null)
            throw new UnauthorizedException("Webhook validation failed");

        // Validate webhook authenticity using the tenant's webhook secret
        var validationContext = new WebhookValidationContext(
            cmd.Headers, cmd.RawBody, cmd.SourceIpAddress);

        if (!provider.ValidateWebhook(validationContext))
            throw new UnauthorizedException("Webhook validation failed");

        // Parse the provider-specific payload
        var paymentEvent = provider.ParseWebhookPayload(cmd.RawBody);

        // Only process success events — others are acknowledged but not acted on
        if (paymentEvent.EventType != PaymentEventType.Success)
            return;

        // Find booking by provider transaction ID (cross-tenant — use tenantId from route)
        var booking = await bookingRepo.GetByPaymentReferenceAsync(
                cmd.TenantId, paymentEvent.ProviderTransactionId, ct)
            ?? throw new NotFoundException("Booking",
                $"PaymentReference={paymentEvent.ProviderTransactionId}");

        // Look up the booking type for the notification slug
        var bookingType = await bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, ct);

        var from = booking.Status;
        booking.Pay("payment-webhook", cmd.ProviderName);

        await bookingRepo.UpdateAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await publisher.Publish(
            new Notifications.BookingStatusChangedNotification(
                BookingId: booking.Id,
                TenantId: booking.TenantId,
                BookingTypeId: booking.BookingTypeId,
                BookingTypeSlug: bookingType?.Slug ?? "unknown",
                FromStatus: from,
                ToStatus: booking.Status,
                Start: booking.Start,
                End: booking.End,
                CustomerId: booking.CustomerId,
                CustomerEmail: booking.CustomerEmail),
            ct);
    }
}
```

**Step 4: Run unit tests**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ProcessPaymentWebhookHandlerTests" -v
```

Expected: PASS (all 3 tests green)

**Step 5: Commit**

```bash
git add src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs \
        tests/Chronith.Tests.Unit/Application/ProcessPaymentWebhookHandlerTests.cs
git commit -m "feat(application): per-tenant payment webhook handler using resolver"
```

---

## Task 2: Update the webhook endpoint route

**Files:**

- Modify: `src/Chronith.API/Endpoints/Payments/PaymentWebhookEndpoint.cs`

**Context:**  
Change the route from `/webhooks/payments/{provider}` to `/webhooks/payments/{tenantId}/{provider}`. Extract `tenantId` from route and pass it to the command.

**Step 1: Write the failing functional test first**

Open `tests/Chronith.Tests.Functional/Payments/PaymentWebhookAuthTests.cs`.  
Change all occurrences of `/v1/webhooks/payments/Stub` to `/v1/webhooks/payments/{TestConstants.TenantId}/Stub`.  
Change the `UnknownProvider` test URL to `/v1/webhooks/payments/{TestConstants.TenantId}/UnknownProvider`.

The full updated file:

```csharp
using System.Net;
using System.Text;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Payments;

/// <summary>
/// Verifies that the payment webhook endpoint at POST /webhooks/payments/{tenantId}/{provider}
/// is AllowAnonymous — external payment providers send webhooks without JWT tokens.
/// </summary>
[Collection("Functional")]
public sealed class PaymentWebhookAuthTests(FunctionalTestFixture fixture)
{
    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
    }

    private static StringContent WebhookBody() =>
        new("{}", Encoding.UTF8, "application/json");

    [Fact]
    public async Task PaymentWebhook_Anonymous_DoesNotReturn401()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/Stub", WebhookBody());

        ((int)response.StatusCode).Should().NotBe(401,
            "the webhook endpoint is AllowAnonymous and should not require authentication");
    }

    [Fact]
    public async Task PaymentWebhook_Anonymous_DoesNotReturn403()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/Stub", WebhookBody());

        ((int)response.StatusCode).Should().NotBe(403,
            "the webhook endpoint is AllowAnonymous and should not check roles");
    }

    [Theory]
    [InlineData("TenantAdmin")]
    [InlineData("TenantStaff")]
    [InlineData("Customer")]
    [InlineData("TenantPaymentService")]
    public async Task PaymentWebhook_AnyRole_DoesNotReturn401Or403(string role)
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient(role);

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/Stub", WebhookBody());

        ((int)response.StatusCode).Should().NotBe(401);
        ((int)response.StatusCode).Should().NotBe(403,
            $"role '{role}' should not be forbidden from calling the webhook endpoint");
    }

    [Fact]
    public async Task PaymentWebhook_UnknownProvider_Returns500OrBadRequest()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.PostAsync(
            $"/v1/webhooks/payments/{TestConstants.TenantId}/UnknownProvider", WebhookBody());

        ((int)response.StatusCode).Should().NotBe(401);
        ((int)response.StatusCode).Should().NotBe(403);
        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400,
            "an unknown provider should result in an error, not a success");
    }
}
```

**Step 2: Run to verify tests fail**

```bash
dotnet test tests/Chronith.Tests.Functional --filter "PaymentWebhookAuthTests" -v
```

Expected: FAIL — the old route `/webhooks/payments/{provider}` returns 404 for the new URLs

**Step 3: Update the endpoint**

Replace the contents of `src/Chronith.API/Endpoints/Payments/PaymentWebhookEndpoint.cs`:

```csharp
using Chronith.Application.Commands.Bookings;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Payments;

public sealed class PaymentWebhookEndpoint(ISender sender)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/webhooks/payments/{tenantId}/{provider}");
        AllowAnonymous(); // Webhooks come from external providers, no JWT
        Options(x => x.WithTags("Payments").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var provider = Route<string>("provider")!;

        // Read raw body for signature verification
        HttpContext.Request.EnableBuffering();
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);

        // Collect headers (lowercased keys for consistent matching)
        var headers = HttpContext.Request.Headers
            .ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value.ToString());

        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        await sender.Send(new ProcessPaymentWebhookCommand
        {
            TenantId = tenantId,
            ProviderName = provider,
            RawBody = rawBody,
            Headers = headers,
            SourceIpAddress = sourceIp
        }, ct);

        await Send.NoContentAsync(ct);
    }
}
```

**Step 4: Run functional tests**

```bash
dotnet test tests/Chronith.Tests.Functional --filter "PaymentWebhookAuthTests" -v
```

Expected: PASS (all 6 tests green)

**Step 5: Run full test suite**

```bash
dotnet test Chronith.slnx
```

Expected: All green. (Unit, integration, functional.)

**Step 6: Commit**

```bash
git add src/Chronith.API/Endpoints/Payments/PaymentWebhookEndpoint.cs \
        tests/Chronith.Tests.Functional/Payments/PaymentWebhookAuthTests.cs
git commit -m "feat(api): per-tenant payment webhook route /webhooks/payments/{tenantId}/{provider}"
```

---

## Task 3: Configure both tenants in production

> **This is a runtime task — no code changes. Run after the branch is merged and deployed.**

### Prerequisites

- The feature branch is merged and deployed to `https://chronith-api.azurewebsites.net`
- You have the PayMongo webhook secret (from the PayMongo dashboard)
- You have decided on `successUrl` and `failureUrl`

### 3a — Register webhook in PayMongo dashboard

For **nexoflow-automations** (tenantId: `1bbf0afa-8a11-42dd-8ef6-ac8879ac140a`):

```
https://chronith-api.azurewebsites.net/v1/webhooks/payments/1bbf0afa-8a11-42dd-8ef6-ac8879ac140a/paymongo
```

For **nexoflow-resort** (tenantId: `97f86bf5-695d-49a7-8f4b-cff050ff1d2d`):

```
https://chronith-api.azurewebsites.net/v1/webhooks/payments/97f86bf5-695d-49a7-8f4b-cff050ff1d2d/paymongo
```

Subscribe to: `checkout_session.payment.paid`, `payment.failed`

Copy the webhook secret shown — it's only visible once.

> Note: if both tenants share one PayMongo account, you can register two separate webhooks (one per URL above) with the **same account**. PayMongo will send to both URLs. Each tenant's `Settings` JSON will have the same `webhookSecret` (since it comes from the same PayMongo account).

### 3b — Create TenantPaymentConfig via API

Login as each tenant admin and call `POST /v1/tenant/payment-config` with the PayMongo settings JSON.

The `Settings` field must be valid JSON matching `PayMongoOptions` (camelCase):

```json
{
  "secretKey": "<sk_test_from_paymongo_dashboard>",
  "publicKey": "<pk_test_from_paymongo_dashboard>",
  "webhookSecret": "<webhook-secret-from-paymongo-dashboard>",
  "successUrl": "<your-success-url>",
  "failureUrl": "<your-failure-url>"
}
```

Then activate via `PATCH /v1/tenant/payment-config/{id}/activate`.

These calls can be scripted and executed by the agent once the webhook secret and redirect URLs are known.

---

## Summary of Changed Files

| File                                                                         | Change                                                                            |
| ---------------------------------------------------------------------------- | --------------------------------------------------------------------------------- |
| `src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs` | Add `TenantId`; swap `IPaymentProviderFactory` → `ITenantPaymentProviderResolver` |
| `src/Chronith.API/Endpoints/Payments/PaymentWebhookEndpoint.cs`              | Route: `/webhooks/payments/{tenantId}/{provider}`                                 |
| `tests/Chronith.Tests.Functional/Payments/PaymentWebhookAuthTests.cs`        | Update URLs to include `tenantId`                                                 |
| `tests/Chronith.Tests.Unit/Application/ProcessPaymentWebhookHandlerTests.cs` | New — handler unit tests                                                          |

No DB migrations. No new repository methods. No interface changes.
