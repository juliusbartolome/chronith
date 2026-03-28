# PaymentFailed Status, Payment Flow Split, Pipeline Logging & Dashboard Pay Button — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Split the payment flow into automatic (webhook → Confirmed) and manual (admin → PendingVerification → Confirmed) paths, add a `PaymentFailed` terminal status, add structured logging across the payment pipeline, and expose a "Mark as Paid" button in the admin dashboard.

**Architecture:** Four features implemented bottom-up (Domain → Application → Infrastructure → API → Dashboard). Feature 1 is the state-machine split + PaymentFailed. Feature 2 (logging) is cross-cutting. Feature 3 (dashboard) is UI. All follow strict TDD.

**Tech Stack:** .NET 10, FastEndpoints, MediatR, FluentValidation, EF Core, xUnit, FluentAssertions, NSubstitute, Next.js (dashboard), TanStack Query.

**Design doc:** `docs/plans/2026-03-26-payment-failed-logging-design.md`

---

## State Machine (Updated)

Two payment paths:

```
AUTOMATIC (webhook from PayMongo/GCash/Maya — trusted source):
  PendingPayment ──ConfirmPayment()──> Confirmed

MANUAL (admin "Mark as Paid" — needs staff verification):
  PendingPayment ──Pay()──> PendingVerification ──Confirm()──> Confirmed

FAILURE:
  PendingPayment ──FailPayment()──> PaymentFailed (terminal)

CANCEL:
  PendingPayment/PendingVerification/Confirmed ──Cancel()──> Cancelled

FREE BOOKINGS:
  Created directly in Confirmed status (skip PendingPayment entirely)
```

Domain methods:

- `Pay()` — PendingPayment → PendingVerification (manual: "payment claimed, needs verification")
- `ConfirmPayment()` — PendingPayment → Confirmed (automatic: "payment confirmed by trusted source")
- `Confirm()` — PendingVerification → Confirmed (staff verifies a manual payment claim)
- `FailPayment()` — PendingPayment → PaymentFailed (terminal)
- `Cancel()` — any non-terminal → Cancelled

---

## Pre-flight: Branch Setup

```bash
git checkout main && git pull
git checkout -b feat/payment-failed-logging
# Copy design doc + plan from the other branch
git checkout feat/booking-reference-id -- docs/plans/2026-03-26-payment-failed-logging-design.md
git add docs/plans/2026-03-26-payment-failed-logging-design.md docs/plans/2026-03-26-payment-failed-logging-plan.md
git commit -m "docs: add payment failed + logging design and plan"
```

---

## Task 1: Add `PaymentFailed` to `BookingStatus` enum

**Files:**

- Modify: `src/Chronith.Domain/Enums/BookingStatus.cs`

**Step 1: Add the enum value**

```csharp
using System.Text.Json.Serialization;

namespace Chronith.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingStatus { PendingPayment, PendingVerification, Confirmed, Cancelled, PaymentFailed }
```

Note: `PaymentFailed` gets ordinal 4. EF column is `string(30)` — `"PaymentFailed"` (13 chars) fits.

**Step 2: Verify build compiles**

```bash
dotnet build Chronith.slnx
```

Expected: Build succeeds.

**Step 3: Commit**

```bash
git add src/Chronith.Domain/Enums/BookingStatus.cs
git commit -m "feat(domain): add PaymentFailed value to BookingStatus enum"
```

---

## Task 2: Add `ConfirmPayment()`, `FailPayment()` domain methods + update guards + free bookings

This is the core state-machine change. Three things happen:

1. New `ConfirmPayment()` method: PendingPayment → Confirmed (for automated/trusted payments)
2. New `FailPayment()` method: PendingPayment → PaymentFailed (terminal)
3. Updated guards: Cancel/AssignStaff/Reschedule reject `PaymentFailed`
4. Free bookings: `Booking.Create()` with amount=0 starts at `Confirmed` instead of `PendingVerification`

**Files:**

- Modify: `src/Chronith.Domain/Models/Booking.cs`
- Modify: `tests/Chronith.Tests.Unit/Helpers/BookingBuilder.cs`
- Test: `tests/Chronith.Tests.Unit/Domain/BookingStateMachineTests.cs`

**Step 1: Write failing tests**

Add to `BookingStateMachineTests.cs`:

```csharp
// ── ConfirmPayment transitions (automated payment path) ──────────────────

[Fact]
public void ConfirmPayment_From_PendingPayment_Transitions_To_Confirmed()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

    booking.ConfirmPayment("payment-webhook", "PayMongo");

    booking.Status.Should().Be(BookingStatus.Confirmed);
}

[Fact]
public void ConfirmPayment_From_PendingVerification_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PendingVerification).Build();

    var act = () => booking.ConfirmPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void ConfirmPayment_From_Confirmed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();

    var act = () => booking.ConfirmPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void ConfirmPayment_From_Cancelled_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();

    var act = () => booking.ConfirmPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void ConfirmPayment_From_PaymentFailed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PaymentFailed).Build();

    var act = () => booking.ConfirmPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void ConfirmPayment_Appends_StatusChange_With_Correct_FromAndTo()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

    booking.ConfirmPayment("payment-webhook", "PayMongo");

    booking.StatusChanges.Should().ContainSingle();
    var change = booking.StatusChanges[0];
    change.FromStatus.Should().Be(BookingStatus.PendingPayment);
    change.ToStatus.Should().Be(BookingStatus.Confirmed);
}

// ── FailPayment transitions ──────────────────────────────────────────────

[Fact]
public void FailPayment_From_PendingPayment_Transitions_To_PaymentFailed()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

    booking.FailPayment("payment-webhook", "PayMongo");

    booking.Status.Should().Be(BookingStatus.PaymentFailed);
}

[Fact]
public void FailPayment_From_PendingVerification_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PendingVerification).Build();

    var act = () => booking.FailPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void FailPayment_From_Confirmed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.Confirmed).Build();

    var act = () => booking.FailPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void FailPayment_From_Cancelled_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.Cancelled).Build();

    var act = () => booking.FailPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void FailPayment_From_PaymentFailed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PaymentFailed).Build();

    var act = () => booking.FailPayment("payment-webhook", "PayMongo");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void FailPayment_Appends_StatusChange_With_Correct_FromAndTo()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PendingPayment).Build();

    booking.FailPayment("payment-webhook", "PayMongo");

    booking.StatusChanges.Should().ContainSingle();
    var change = booking.StatusChanges[0];
    change.FromStatus.Should().Be(BookingStatus.PendingPayment);
    change.ToStatus.Should().Be(BookingStatus.PaymentFailed);
}

// ── PaymentFailed is terminal — rejects Cancel, AssignStaff, Reschedule ──

[Fact]
public void Cancel_From_PaymentFailed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PaymentFailed).Build();

    var act = () => booking.Cancel("user-1", "admin");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void AssignStaff_From_PaymentFailed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PaymentFailed).Build();

    var act = () => booking.AssignStaff(Guid.NewGuid(), "user-1", "admin");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void Reschedule_From_PaymentFailed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PaymentFailed).Build();

    var act = () => booking.Reschedule(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "user-1", "admin");

    act.Should().Throw<InvalidStateTransitionException>();
}

[Fact]
public void Pay_From_PaymentFailed_Throws_InvalidStateTransitionException()
{
    var booking = new BookingBuilder().InStatus(BookingStatus.PaymentFailed).Build();

    var act = () => booking.Pay("user-1", "admin");

    act.Should().Throw<InvalidStateTransitionException>();
}
```

Update the **existing** free booking test — change expected starting status from `PendingVerification` to `Confirmed`:

```csharp
[Fact]
public void Create_WithZeroAmount_Starts_In_Confirmed()
{
    var booking = Booking.Create(
        tenantId: Guid.NewGuid(),
        bookingTypeId: Guid.NewGuid(),
        start: DateTimeOffset.UtcNow,
        end: DateTimeOffset.UtcNow.AddHours(1),
        customerId: "cust-1",
        customerEmail: "cust@example.com",
        amountInCentavos: 0,
        currency: "PHP");

    booking.Status.Should().Be(BookingStatus.Confirmed);
    booking.AmountInCentavos.Should().Be(0);
}
```

Note: The existing test `Create_WithZeroAmount_Starts_In_PendingVerification` should be renamed and updated to expect `Confirmed`.

**Step 2: Update `BookingBuilder` to support `PaymentFailed` status**

In `tests/Chronith.Tests.Unit/Helpers/BookingBuilder.cs`:

Update `InStatus` — add `PaymentFailed` to the list that sets `_amountInCentavos = 10000`:

```csharp
if (status == BookingStatus.PendingPayment ||
    status == BookingStatus.PendingVerification ||
    status == BookingStatus.Confirmed ||
    status == BookingStatus.PaymentFailed)
{
    _amountInCentavos = 10000;
}
```

Update `Build()` switch — add `PaymentFailed` case:

```csharp
case BookingStatus.PaymentFailed:
    booking.FailPayment("test-user", "test-role");
    break;
```

**Step 3: Run tests to verify they fail**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "BookingStateMachineTests"
```

Expected: Compilation errors — `ConfirmPayment` and `FailPayment` don't exist yet.

**Step 4: Implement domain changes**

In `src/Chronith.Domain/Models/Booking.cs`:

**4a.** Update `Booking.Create()` — change free booking starting status from `PendingVerification` to `Confirmed`:

```csharp
Status = isFree ? BookingStatus.Confirmed : BookingStatus.PendingPayment,
```

**4b.** Add `ConfirmPayment()` after `Pay()` (after line 86):

```csharp
public void ConfirmPayment(string changedById, string changedByRole)
{
    if (Status != BookingStatus.PendingPayment)
        throw new InvalidStateTransitionException(Status, "confirm payment");
    Transition(BookingStatus.Confirmed, changedById, changedByRole);
}
```

**4c.** Add `FailPayment()` after `ConfirmPayment()`:

```csharp
public void FailPayment(string changedById, string changedByRole)
{
    if (Status != BookingStatus.PendingPayment)
        throw new InvalidStateTransitionException(Status, "fail payment");
    Transition(BookingStatus.PaymentFailed, changedById, changedByRole);
}
```

**4d.** Update `Cancel()` guard — also reject `PaymentFailed`:

```csharp
public void Cancel(string changedById, string changedByRole)
{
    if (Status is BookingStatus.Cancelled or BookingStatus.PaymentFailed)
        throw new InvalidStateTransitionException(Status, "cancel");
    Transition(BookingStatus.Cancelled, changedById, changedByRole);
}
```

**4e.** Update `AssignStaff()` guard — also reject `PaymentFailed`:

```csharp
public void AssignStaff(Guid staffMemberId, string changedById, string changedByRole)
{
    if (Status is BookingStatus.Cancelled or BookingStatus.PaymentFailed)
        throw new InvalidStateTransitionException(Status, "assign staff");
    StaffMemberId = staffMemberId;
}
```

**4f.** Update `Reschedule()` guard — also reject `PaymentFailed`:

```csharp
public void Reschedule(DateTimeOffset newStart, DateTimeOffset newEnd, string changedById, string changedByRole)
{
    if (Status is BookingStatus.Cancelled or BookingStatus.PaymentFailed)
        throw new InvalidStateTransitionException(Status, "reschedule");
    Start = newStart;
    End = newEnd;
    _statusChanges.Add(new BookingStatusChange(Id, Status, Status, changedById, changedByRole));
}
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "BookingStateMachineTests"
```

Expected: All pass. Some existing tests that were building bookings in PendingVerification via the free path may need updating if they relied on `Create(amount=0)` producing `PendingVerification`. The `BookingBuilder` uses `InStatus()` which sets amount=10000 for non-free statuses, so it should be fine — it transitions through `Pay()`.

**Step 6: Run full unit tests to check for breakage from free booking change**

```bash
dotnet test tests/Chronith.Tests.Unit
```

Check for any test that creates a booking with `amountInCentavos: 0` and expects `PendingVerification`. Fix those to expect `Confirmed`.

**Step 7: Commit**

```bash
git add src/Chronith.Domain/Models/Booking.cs \
        tests/Chronith.Tests.Unit/Domain/BookingStateMachineTests.cs \
        tests/Chronith.Tests.Unit/Helpers/BookingBuilder.cs
git commit -m "feat(domain): add ConfirmPayment() and FailPayment() methods, update guards, free bookings start Confirmed"
```

---

## Task 3: Update webhook handler to use `ConfirmPayment()` for success, `FailPayment()` for failure

The webhook handler currently calls `booking.Pay()` for success events. It needs to call `booking.ConfirmPayment()` instead (trusted automated payment → skip verification). For failed events, call `booking.FailPayment()`.

**Files:**

- Modify: `src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs`
- Test: `tests/Chronith.Tests.Unit/Application/ProcessPaymentWebhookHandlerTests.cs`

**Step 1: Write failing tests**

Remove the existing test `Handle_WhenEventIsNotSuccess_DoesNotUpdateBooking` — the handler will now process failed events.

Add these new tests to `ProcessPaymentWebhookHandlerTests.cs`:

```csharp
[Fact]
public async Task Handle_WhenEventIsSuccess_TransitionsBookingToConfirmed()
{
    var provider = Substitute.For<IPaymentProvider>();
    provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
    provider.ParseWebhookPayload(Arg.Any<string>())
        .Returns(new WebhookPaymentEvent("ref-123", PaymentEventType.Success));

    _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
        .Returns(provider);

    var booking = new BookingBuilder()
        .InStatus(BookingStatus.PendingPayment)
        .WithPaymentReference("ref-123")
        .Build();

    _bookingRepo.GetByPaymentReferenceAsync(TenantId, "ref-123", Arg.Any<CancellationToken>())
        .Returns(booking);

    _bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, Arg.Any<CancellationToken>())
        .ReturnsNull();

    var cmd = new ProcessPaymentWebhookCommand
    {
        TenantId = TenantId,
        ProviderName = "PayMongo",
        RawBody = "{}",
        Headers = new Dictionary<string, string>()
    };

    await _handler.Handle(cmd, CancellationToken.None);

    booking.Status.Should().Be(BookingStatus.Confirmed);
    await _bookingRepo.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
    await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    await _publisher.Received(1).Publish(
        Arg.Is<BookingStatusChangedNotification>(n =>
            n.ToStatus == BookingStatus.Confirmed),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task Handle_WhenEventIsFailed_TransitionsBookingToPaymentFailed()
{
    var provider = Substitute.For<IPaymentProvider>();
    provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
    provider.ParseWebhookPayload(Arg.Any<string>())
        .Returns(new WebhookPaymentEvent("ref-123", PaymentEventType.Failed));

    _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
        .Returns(provider);

    var booking = new BookingBuilder()
        .InStatus(BookingStatus.PendingPayment)
        .WithPaymentReference("ref-123")
        .Build();

    _bookingRepo.GetByPaymentReferenceAsync(TenantId, "ref-123", Arg.Any<CancellationToken>())
        .Returns(booking);

    _bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, Arg.Any<CancellationToken>())
        .ReturnsNull();

    var cmd = new ProcessPaymentWebhookCommand
    {
        TenantId = TenantId,
        ProviderName = "PayMongo",
        RawBody = "{}",
        Headers = new Dictionary<string, string>()
    };

    await _handler.Handle(cmd, CancellationToken.None);

    booking.Status.Should().Be(BookingStatus.PaymentFailed);
    await _bookingRepo.Received(1).UpdateAsync(booking, Arg.Any<CancellationToken>());
    await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    await _publisher.Received(1).Publish(
        Arg.Is<BookingStatusChangedNotification>(n =>
            n.ToStatus == BookingStatus.PaymentFailed),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task Handle_WhenEventIsFailed_AndBookingNotFound_ThrowsNotFoundException()
{
    var provider = Substitute.For<IPaymentProvider>();
    provider.ValidateWebhook(Arg.Any<WebhookValidationContext>()).Returns(true);
    provider.ParseWebhookPayload(Arg.Any<string>())
        .Returns(new WebhookPaymentEvent("ref-missing", PaymentEventType.Failed));

    _resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
        .Returns(provider);

    _bookingRepo.GetByPaymentReferenceAsync(TenantId, "ref-missing", Arg.Any<CancellationToken>())
        .ReturnsNull();

    var cmd = new ProcessPaymentWebhookCommand
    {
        TenantId = TenantId,
        ProviderName = "PayMongo",
        RawBody = "{}",
        Headers = new Dictionary<string, string>()
    };

    var act = () => _handler.Handle(cmd, CancellationToken.None);

    await act.Should().ThrowAsync<NotFoundException>();
}
```

You'll need to add `using Chronith.Tests.Unit.Helpers;` to imports in the test file.

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ProcessPaymentWebhookHandlerTests"
```

Expected: `Handle_WhenEventIsSuccess_TransitionsBookingToConfirmed` fails because handler calls `Pay()` (→ PendingVerification) not `ConfirmPayment()` (→ Confirmed). The failed-event tests fail because handler currently returns early.

**Step 3: Implement the handler change**

In `src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs`, replace lines 60-94 (from `// Only process success events` through end of method) with:

```csharp
        // Find booking by provider transaction ID (scoped to the tenant from the route)
        var booking = await bookingRepo.GetByPaymentReferenceAsync(
                cmd.TenantId, paymentEvent.ProviderTransactionId, ct)
            ?? throw new NotFoundException("Booking",
                $"PaymentReference={paymentEvent.ProviderTransactionId}");

        // Look up the booking type for the notification slug
        var bookingType = await bookingTypeRepo.GetByIdAsync(booking.BookingTypeId, ct);

        var from = booking.Status;

        if (paymentEvent.EventType == PaymentEventType.Success)
            booking.ConfirmPayment("payment-webhook", cmd.ProviderName);
        else
            booking.FailPayment("payment-webhook", cmd.ProviderName);

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
                CustomerEmail: booking.CustomerEmail,
                CustomerFirstName: booking.FirstName,
                CustomerLastName: booking.LastName,
                CustomerMobile: booking.Mobile),
            ct);
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ProcessPaymentWebhookHandlerTests"
```

Expected: All pass.

**Step 5: Commit**

```bash
git add src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs \
        tests/Chronith.Tests.Unit/Application/ProcessPaymentWebhookHandlerTests.cs
git commit -m "feat(app): webhook uses ConfirmPayment() for success, FailPayment() for failure"
```

---

## Task 4: Add `PaymentFailed` to webhook/notification outbox handlers

**Files:**

- Modify: `src/Chronith.Application/Notifications/WebhookOutboxHandler.cs`
- Modify: `src/Chronith.Application/Notifications/NotificationOutboxHandler.cs`
- Test: `tests/Chronith.Tests.Unit/Application/WebhookOutboxHandlerTests.cs`

**Step 1: Write failing test for WebhookOutboxHandler**

Read the existing `WebhookOutboxHandlerTests.cs` to understand the pattern, then add a test for `PaymentFailed`:

```csharp
[Fact]
public async Task Handle_PaymentFailed_EnqueuesTenantWebhook_WithCorrectEventType()
{
    // Arrange — set up notification with ToStatus = PaymentFailed
    // Follow the same pattern as existing tests in this file
    // Assert the outbox entry has EventType = "booking.payment_failed"
    // and customer callback has EventType = "customer.payment.failed"
}
```

(Read the existing test file to match exact patterns before writing the test.)

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "WebhookOutboxHandlerTests"
```

**Step 3: Implement the change in WebhookOutboxHandler**

In `src/Chronith.Application/Notifications/WebhookOutboxHandler.cs`, update the two switch expressions:

Tenant event type switch (lines 20-26):

```csharp
var tenantEventType = notification.ToStatus switch
{
    BookingStatus.PendingVerification => "booking.payment_received",
    BookingStatus.Confirmed           => "booking.confirmed",
    BookingStatus.Cancelled           => "booking.cancelled",
    BookingStatus.PaymentFailed       => "booking.payment_failed",
    _                                 => null
};
```

Customer event type switch (lines 28-34):

```csharp
var customerEventType = notification.ToStatus switch
{
    BookingStatus.PendingVerification => "customer.payment.received",
    BookingStatus.Confirmed           => "customer.booking.confirmed",
    BookingStatus.Cancelled           => "customer.booking.cancelled",
    BookingStatus.PaymentFailed       => "customer.payment.failed",
    _                                 => null
};
```

**Step 4: Implement the change in NotificationOutboxHandler**

In `src/Chronith.Application/Notifications/NotificationOutboxHandler.cs`, update the switch (lines 19-25):

```csharp
var eventType = notification.ToStatus switch
{
    BookingStatus.PendingVerification => "notification.payment_received",
    BookingStatus.Confirmed           => "notification.booking_confirmed",
    BookingStatus.Cancelled           => "notification.booking_cancelled",
    BookingStatus.PaymentFailed       => "notification.payment_failed",
    _                                 => null
};
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "WebhookOutboxHandler"
```

**Step 6: Commit**

```bash
git add src/Chronith.Application/Notifications/WebhookOutboxHandler.cs \
        src/Chronith.Application/Notifications/NotificationOutboxHandler.cs \
        tests/Chronith.Tests.Unit/Application/WebhookOutboxHandlerTests.cs
git commit -m "feat(app): add PaymentFailed case to webhook and notification outbox handlers"
```

---

## Task 5: Add structured logging to `ProcessPaymentWebhookHandler`

**Files:**

- Modify: `src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs`
- Modify: `tests/Chronith.Tests.Unit/Application/ProcessPaymentWebhookHandlerTests.cs`

**Step 1: Add `ILogger<ProcessPaymentWebhookHandler>` to primary constructor**

```csharp
public sealed class ProcessPaymentWebhookHandler(
    ITenantPaymentProviderResolver resolver,
    IBookingRepository bookingRepo,
    IBookingTypeRepository bookingTypeRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    ILogger<ProcessPaymentWebhookHandler> logger)
    : IRequestHandler<ProcessPaymentWebhookCommand>
```

Add `using Microsoft.Extensions.Logging;` to imports.

**Step 2: Add log statements throughout the handler**

After resolving provider:

```csharp
logger.LogInformation("Payment provider resolved: {ProviderName} for tenant {TenantId}", cmd.ProviderName, cmd.TenantId);
```

When resolver returns null (before throwing):

```csharp
logger.LogWarning("Failed to resolve payment provider {ProviderName} for tenant {TenantId}", cmd.ProviderName, cmd.TenantId);
```

When validation fails (before throwing):

```csharp
logger.LogWarning("Webhook signature validation failed for {TenantId}/{ProviderName}", cmd.TenantId, cmd.ProviderName);
```

After parsing event:

```csharp
logger.LogInformation("Parsed payment event: {EventType}, transaction {ProviderTransactionId}", paymentEvent.EventType, paymentEvent.ProviderTransactionId);
```

When booking not found (before throwing):

```csharp
logger.LogWarning("Booking not found for payment reference {ProviderTransactionId} on tenant {TenantId}", paymentEvent.ProviderTransactionId, cmd.TenantId);
```

For failed events specifically (before calling FailPayment):

```csharp
logger.LogWarning("Non-success payment event {EventType} for transaction {ProviderTransactionId} — transitioning to PaymentFailed", paymentEvent.EventType, paymentEvent.ProviderTransactionId);
```

After state transition:

```csharp
logger.LogInformation("Booking {BookingId} transitioned from {FromStatus} to {ToStatus}", booking.Id, from, booking.Status);
```

**Step 3: Update the test constructor to inject logger**

In `ProcessPaymentWebhookHandlerTests.cs`:

- Add field: `private readonly ILogger<ProcessPaymentWebhookHandler> _logger;`
- In constructor: `_logger = Substitute.For<ILogger<ProcessPaymentWebhookHandler>>();`
- Update handler creation: `_handler = new ProcessPaymentWebhookHandler(_resolver, _bookingRepo, _bookingTypeRepo, _unitOfWork, _publisher, _logger);`
- Add `using Microsoft.Extensions.Logging;`

**Step 4: Run all tests**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ProcessPaymentWebhookHandlerTests"
```

**Step 5: Commit**

```bash
git add src/Chronith.Application/Commands/Bookings/ProcessPaymentWebhookCommand.cs \
        tests/Chronith.Tests.Unit/Application/ProcessPaymentWebhookHandlerTests.cs
git commit -m "feat(app): add structured logging to ProcessPaymentWebhookHandler"
```

---

## Task 6: Add structured logging to `PaymentWebhookEndpoint`

**Files:**

- Modify: `src/Chronith.API/Endpoints/Payments/PaymentWebhookEndpoint.cs`

**Step 1: Add `ILogger<PaymentWebhookEndpoint>` and log statement**

```csharp
public sealed class PaymentWebhookEndpoint(ISender sender, ILogger<PaymentWebhookEndpoint> logger)
    : EndpointWithoutRequest
```

After extracting tenantId, provider, and sourceIp:

```csharp
logger.LogInformation("Webhook received: tenant {TenantId}, provider {Provider}, source {SourceIp}",
    tenantId, provider, sourceIp);
```

Add `using Microsoft.Extensions.Logging;`.

**Step 2: Verify build**

```bash
dotnet build Chronith.slnx
```

**Step 3: Commit**

```bash
git add src/Chronith.API/Endpoints/Payments/PaymentWebhookEndpoint.cs
git commit -m "feat(api): add structured logging to PaymentWebhookEndpoint"
```

---

## Task 7: Add structured logging to `PayMongoProvider` and `TenantPaymentProviderResolver`

**Files:**

- Modify: `src/Chronith.Infrastructure/Payments/PayMongo/PayMongoProvider.cs`
- Modify: `src/Chronith.Infrastructure/Payments/TenantPaymentProviderResolver.cs`

**Step 1: Add `ILogger<PayMongoProvider>` to PayMongoProvider**

```csharp
public sealed class PayMongoProvider(
    IOptions<PayMongoOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<PayMongoProvider> logger)
    : IPaymentProvider
```

Add log statements:

- `ValidateWebhook`: Log warning on missing `paymongo-signature` header
- `ValidateWebhookSignature`: Log warning on timestamp outside tolerance, signature mismatch, and caught exceptions
- `ParseWebhookPayload`: Log debug on unknown event type mapped to Failed
- `CreateCheckoutSessionAsync`: Log info on checkout created, log error on API failure

For the API error, change `response.EnsureSuccessStatusCode()` to:

```csharp
if (!response.IsSuccessStatusCode)
{
    var errorBody = await response.Content.ReadAsStringAsync(ct);
    logger.LogError("PayMongo API error: {StatusCode} {ErrorBody}", response.StatusCode, errorBody);
    response.EnsureSuccessStatusCode();
}
```

**Step 2: Update `TenantPaymentProviderResolver` to use `ILoggerFactory`**

```csharp
public sealed class TenantPaymentProviderResolver(
    ITenantPaymentConfigRepository configRepo,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory)
    : ITenantPaymentProviderResolver
{
    private readonly ILogger<TenantPaymentProviderResolver> _logger =
        loggerFactory.CreateLogger<TenantPaymentProviderResolver>();
```

Add log statements:

- `ResolveAsync`: Log info on successful resolution, warning on null config, warning on unknown provider name
- Pass `loggerFactory.CreateLogger<PayMongoProvider>()` to `BuildPayMongo`

Update `BuildPayMongo`:

```csharp
private IPaymentProvider BuildPayMongo(string settings)
{
    var opts = JsonSerializer.Deserialize<PayMongoOptions>(settings, JsonOpts) ?? new PayMongoOptions();
    return new PayMongoProvider(Options.Create(opts), httpClientFactory,
        loggerFactory.CreateLogger<PayMongoProvider>());
}
```

**Step 3: Verify build**

```bash
dotnet build Chronith.slnx
```

**Step 4: Commit**

```bash
git add src/Chronith.Infrastructure/Payments/PayMongo/PayMongoProvider.cs \
        src/Chronith.Infrastructure/Payments/TenantPaymentProviderResolver.cs
git commit -m "feat(infra): add structured logging to PayMongoProvider and TenantPaymentProviderResolver"
```

---

## Task 8: Add structured logging to `CreatePublicCheckoutHandler`

**Files:**

- Modify: `src/Chronith.Application/Commands/Public/CreatePublicCheckoutCommand.cs`

**Step 1: Add logger and log statements**

Add `ILogger<CreatePublicCheckoutHandler> logger` to primary constructor. Add `using Microsoft.Extensions.Logging;`.

Log statements:

- At start: `"Creating checkout for booking {BookingId}, provider {ProviderName}"`
- After URL resolution: `"URL resolution: using {Tier} (request > tenant-config > global)"` where Tier is computed
- After checkout created: `"Checkout session created, redirecting to {ProviderName}"`

**Step 2: Verify build**

```bash
dotnet build Chronith.slnx
```

**Step 3: Commit**

```bash
git add src/Chronith.Application/Commands/Public/CreatePublicCheckoutCommand.cs
git commit -m "feat(app): add structured logging to CreatePublicCheckoutHandler"
```

---

## Task 9: Add `PaymentFailed` badge to dashboard

**Files:**

- Modify: `dashboard/src/components/bookings/booking-status-badge.tsx`

**Step 1: Add `PaymentFailed` to `STATUS_CONFIG`**

```tsx
const STATUS_CONFIG = {
  PendingPayment: { label: "Pending Payment", variant: "outline" as const },
  PendingVerification: {
    label: "Pending Verification",
    variant: "secondary" as const,
  },
  Confirmed: { label: "Confirmed", variant: "default" as const },
  Cancelled: { label: "Cancelled", variant: "destructive" as const },
  PaymentFailed: { label: "Payment Failed", variant: "destructive" as const },
};
```

**Step 2: Verify dashboard builds**

```bash
npm run build
```

(Run from `dashboard/` directory)

**Step 3: Commit**

```bash
git add dashboard/src/components/bookings/booking-status-badge.tsx
git commit -m "feat(dashboard): add PaymentFailed variant to booking status badge"
```

---

## Task 10: Add "Mark as Paid" button to dashboard

This button calls `POST /v1/bookings/{id}/pay` which triggers `Pay()` → PendingVerification (manual path).

**Files:**

- Create: `dashboard/src/app/api/bookings/[id]/pay/route.ts`
- Modify: `dashboard/src/hooks/use-bookings.ts`
- Modify: `dashboard/src/app/(dashboard)/bookings/[id]/page.tsx`

**Step 1: Create the API proxy route**

Create `dashboard/src/app/api/bookings/[id]/pay/route.ts`:

```typescript
import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  return proxyToApi(request, `/v1/bookings/${id}/pay`, { method: "POST" });
}
```

**Step 2: Add `usePayBooking` hook**

In `dashboard/src/hooks/use-bookings.ts`, add after `useCancelBooking`:

```typescript
export function usePayBooking() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/bookings/${id}/pay`, {
        method: "POST",
      });
      if (!res.ok) throw new Error("Failed to mark booking as paid");
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["bookings"] });
    },
  });
}
```

**Step 3: Add "Mark as Paid" button to booking detail page**

In `dashboard/src/app/(dashboard)/bookings/[id]/page.tsx`:

Update import to include `usePayBooking`.

Add hook call: `const pay = usePayBooking();`

Add button in the actions `<div className="flex gap-2">`, before the Confirm button:

```tsx
{
  booking.status === "PendingPayment" && (
    <Button
      size="sm"
      variant="secondary"
      onClick={() => pay.mutate(id)}
      disabled={pay.isPending}
    >
      Mark as Paid
    </Button>
  );
}
```

**Step 4: Verify dashboard builds**

```bash
npm run build
```

(Run from `dashboard/` directory)

**Step 5: Commit**

```bash
git add dashboard/src/app/api/bookings/[id]/pay/route.ts \
        dashboard/src/hooks/use-bookings.ts \
        dashboard/src/app/(dashboard)/bookings/[id]/page.tsx
git commit -m "feat(dashboard): add Mark as Paid button for PendingPayment bookings"
```

---

## Task 11: Update C# SDK

**Files:**

- Modify: `packages/sdk-csharp/src/Chronith.Client/Models/BookingDto.cs` (if needed)

The C# SDK stores `Status` as `string` (not enum). Check if there's a separate `BookingStatus` enum:

```bash
grep -r "enum BookingStatus" packages/sdk-csharp/
```

If no enum exists, skip — the string `"PaymentFailed"` / `"Confirmed"` just works.
If an enum exists, add `PaymentFailed` to it.

Commit if needed:

```bash
git add packages/sdk-csharp/
git commit -m "feat(sdk): add PaymentFailed to BookingStatus enum"
```

---

## Task 12: Full test suite verification + design doc update

**Step 1: Run all unit tests**

```bash
dotnet test tests/Chronith.Tests.Unit
```

Expected: All pass.

**Step 2: Build entire solution**

```bash
dotnet build Chronith.slnx
```

Expected: Build succeeds.

**Step 3: Build dashboard**

```bash
cd dashboard && npm run build
```

Expected: Build succeeds.

**Step 4: Update design doc to reflect the final state machine**

Update `docs/plans/2026-03-26-payment-failed-logging-design.md` to document the split payment flow (automatic vs manual paths), `ConfirmPayment()` method, and free bookings starting at `Confirmed`.

**Step 5: Commit design doc update**

```bash
git add docs/plans/2026-03-26-payment-failed-logging-design.md
git commit -m "docs: update design doc with split payment flow and ConfirmPayment()"
```

---

## Summary of Commits

1. `docs: add payment failed + logging design and plan`
2. `feat(domain): add PaymentFailed value to BookingStatus enum`
3. `feat(domain): add ConfirmPayment() and FailPayment() methods, update guards, free bookings start Confirmed`
4. `feat(app): webhook uses ConfirmPayment() for success, FailPayment() for failure`
5. `feat(app): add PaymentFailed case to webhook and notification outbox handlers`
6. `feat(app): add structured logging to ProcessPaymentWebhookHandler`
7. `feat(api): add structured logging to PaymentWebhookEndpoint`
8. `feat(infra): add structured logging to PayMongoProvider and TenantPaymentProviderResolver`
9. `feat(app): add structured logging to CreatePublicCheckoutHandler`
10. `feat(dashboard): add PaymentFailed variant to booking status badge`
11. `feat(dashboard): add Mark as Paid button for PendingPayment bookings`
12. `feat(sdk): add PaymentFailed to BookingStatus enum` (if applicable)
13. `docs: update design doc with split payment flow and ConfirmPayment()`
