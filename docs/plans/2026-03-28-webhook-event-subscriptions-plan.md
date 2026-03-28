# Webhook Event Subscriptions — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow tenants to specify which webhook event names each registered webhook receives, so only matching events are dispatched.

**Architecture:** Add a `WebhookEventTypes` collection to the `Webhook` domain model, persist via a `webhook_event_subscriptions` join table, filter outbox entries in `WebhookOutboxHandler` by subscribed events, and expose event types in create/update/list APIs.

**Tech Stack:** .NET 10, EF Core 10, FastEndpoints 8, MediatR 14, FluentValidation 12, PostgreSQL 17, xUnit, FluentAssertions, NSubstitute

---

### Task 1: Domain — Add `WebhookEventTypes` constants and `Webhook` model changes

**Files:**

- Create: `src/Chronith.Domain/Models/WebhookEventTypes.cs`
- Modify: `src/Chronith.Domain/Models/Webhook.cs`

**Step 1: Create `WebhookEventTypes` constants class**

```csharp
// src/Chronith.Domain/Models/WebhookEventTypes.cs
namespace Chronith.Domain.Models;

public static class WebhookEventTypes
{
    public const string PaymentReceived = "booking.payment_received";
    public const string Confirmed = "booking.confirmed";
    public const string Cancelled = "booking.cancelled";
    public const string PaymentFailed = "booking.payment_failed";

    public static readonly IReadOnlyList<string> All =
    [
        PaymentReceived,
        Confirmed,
        Cancelled,
        PaymentFailed
    ];

    public static bool IsValid(string eventType) => All.Contains(eventType);
}
```

**Step 2: Add `EventTypes` collection to `Webhook`**

Modify `src/Chronith.Domain/Models/Webhook.cs`:

- Add private `List<string> _eventTypes` backing field
- Add public `IReadOnlyList<string> EventTypes` read-only property
- Update `Create(...)` to accept `IReadOnlyList<string> eventTypes` parameter, validate non-empty + all valid
- Add `UpdateSubscriptions(IReadOnlyList<string> eventTypes)` method
- Add `Update(string? url, string? secret, IReadOnlyList<string>? eventTypes)` for partial updates

**Step 3: Build and verify compilation**

Run: `dotnet build Chronith.slnx`

**Step 4: Commit**

```
feat(domain): add webhook event type subscriptions to Webhook model
```

---

### Task 2: Unit tests — Domain validation for `Webhook` event types

**Files:**

- Create: `tests/Chronith.Tests.Unit/Domain/WebhookTests.cs`

**Step 1: Write failing tests**

Test cases:

- `Create_WithValidEventTypes_SetsEventTypes` — verify round-trip
- `Create_WithEmptyEventTypes_ThrowsArgumentException`
- `Create_WithUnknownEventType_ThrowsArgumentException`
- `Create_WithDuplicateEventTypes_Deduplicates`
- `UpdateSubscriptions_WithValidEventTypes_ReplacesExisting`
- `UpdateSubscriptions_WithEmpty_ThrowsArgumentException`
- `Update_WithEventTypes_ReplacesSubscriptions`
- `Update_WithNullEventTypes_PreservesExisting`

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~WebhookTests"`

**Step 3: Fix any compilation issues from Task 1 that prevent tests from building**

**Step 4: Run tests to verify they pass**

**Step 5: Commit**

```
test: add unit tests for Webhook event type subscriptions
```

---

### Task 3: Infrastructure — Entity, configuration, migration

**Files:**

- Create: `src/Chronith.Infrastructure/Persistence/Entities/WebhookEventSubscriptionEntity.cs`
- Create: `src/Chronith.Infrastructure/Persistence/Configurations/WebhookEventSubscriptionConfiguration.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Entities/WebhookEntity.cs` — add navigation
- Modify: `src/Chronith.Infrastructure/Persistence/Configurations/WebhookConfiguration.cs` — add HasMany
- Modify: `src/Chronith.Infrastructure/Persistence/ChronithDbContext.cs` — add DbSet

**Step 1: Create `WebhookEventSubscriptionEntity`**

```csharp
namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class WebhookEventSubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid WebhookId { get; set; }
    public string EventName { get; set; } = string.Empty;
}
```

**Step 2: Add navigation property to `WebhookEntity`**

```csharp
public List<WebhookEventSubscriptionEntity> EventSubscriptions { get; set; } = [];
```

**Step 3: Create EF configuration**

```csharp
public sealed class WebhookEventSubscriptionConfiguration
    : IEntityTypeConfiguration<WebhookEventSubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<WebhookEventSubscriptionEntity> builder)
    {
        builder.ToTable("webhook_event_subscriptions", "chronith");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EventName).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => new { e.WebhookId, e.EventName }).IsUnique();
        builder.HasOne<WebhookEntity>()
            .WithMany(w => w.EventSubscriptions)
            .HasForeignKey(e => e.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Step 4: Update `WebhookConfiguration` to include `HasMany`** (if needed beyond the subscription config)

**Step 5: Add `DbSet<WebhookEventSubscriptionEntity>` to `ChronithDbContext`**

**Step 6: Generate EF migration**

```bash
dotnet ef migrations add AddWebhookEventSubscriptions \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

**Step 7: Add data migration SQL to seed existing webhooks with all 4 event types**

In the migration's `Up()` method, after the table creation, add:

```csharp
migrationBuilder.Sql("""
    INSERT INTO chronith.webhook_event_subscriptions ("Id", "WebhookId", "EventName")
    SELECT gen_random_uuid(), w."Id", e."EventName"
    FROM chronith.webhooks w
    CROSS JOIN (VALUES
        ('booking.payment_received'),
        ('booking.confirmed'),
        ('booking.cancelled'),
        ('booking.payment_failed')
    ) AS e("EventName")
    WHERE NOT w."IsDeleted"
    ON CONFLICT DO NOTHING;
    """);
```

**Step 8: Build and verify**

Run: `dotnet build Chronith.slnx`

**Step 9: Commit**

```
feat(infra): add webhook_event_subscriptions table and migration
```

---

### Task 4: Infrastructure — Update `WebhookRepository` to include subscriptions

**Files:**

- Modify: `src/Chronith.Infrastructure/Persistence/Repositories/WebhookRepository.cs`

**Step 1: Update all read queries to `.Include(w => w.EventSubscriptions)`**

**Step 2: Update `MapToDomain` to pass event types from entity subscriptions to domain**

**Step 3: Update `AddAsync` to persist subscription entities alongside the webhook**

**Step 4: Add `UpdateAsync` method for partial updates (URL, secret, event types)**

Use the delete + re-insert pattern for subscriptions:

```csharp
await db.WebhookEventSubscriptions
    .Where(s => s.WebhookId == webhook.Id)
    .ExecuteDeleteAsync(ct);
await db.WebhookEventSubscriptions.AddRangeAsync(
    webhook.EventTypes.Select(e => new WebhookEventSubscriptionEntity
    {
        Id = Guid.NewGuid(), WebhookId = webhook.Id, EventName = e
    }), ct);
```

**Step 5: Update `IWebhookRepository` interface to add `UpdateAsync`**

**Step 6: Build and verify**

**Step 7: Commit**

```
feat(infra): update WebhookRepository to persist event subscriptions
```

---

### Task 5: Application — Update DTOs, mappers, create command, and validator

**Files:**

- Modify: `src/Chronith.Application/DTOs/WebhookDto.cs`
- Modify: `src/Chronith.Application/Mappers/WebhookMapper.cs`
- Modify: `src/Chronith.Application/Commands/Webhooks/CreateWebhookCommand.cs`

**Step 1: Update `WebhookDto` to include `EventTypes`**

```csharp
public sealed record WebhookDto(Guid Id, string Url, IReadOnlyList<string> EventTypes);
```

**Step 2: Update `WebhookMapper.ToDto` to map event types**

**Step 3: Update `CreateWebhookCommand` to add `EventTypes` property**

**Step 4: Update `CreateWebhookValidator` to validate event types: non-empty, all known**

**Step 5: Update `CreateWebhookHandler` to pass event types to `Webhook.Create`**

**Step 6: Build and verify**

**Step 7: Commit**

```
feat(app): add event type subscriptions to webhook create flow
```

---

### Task 6: Application — Add `UpdateWebhookCommand`

**Files:**

- Create: `src/Chronith.Application/Commands/Webhooks/UpdateWebhookCommand.cs`

**Step 1: Create command, validator, handler**

- Command: `BookingTypeSlug`, `WebhookId`, optional `Url`, optional `Secret`, optional `EventTypes`
- Validator: if `Url` provided, must be valid absolute URI; if `Secret` provided, min 16 chars; if `EventTypes` provided, non-empty + all valid
- Handler: fetch webhook, call `webhook.Update(...)`, persist via repository

**Step 2: Build and verify**

**Step 3: Commit**

```
feat(app): add UpdateWebhookCommand for partial webhook updates
```

---

### Task 7: API — Update `CreateWebhookEndpoint`, add `UpdateWebhookEndpoint`

**Files:**

- Modify: `src/Chronith.API/Endpoints/Webhooks/CreateWebhookEndpoint.cs`
- Create: `src/Chronith.API/Endpoints/Webhooks/UpdateWebhookEndpoint.cs`

**Step 1: Update `CreateWebhookRequest` to include `EventTypes` list**

**Step 2: Update `CreateWebhookEndpoint.HandleAsync` to pass event types to command**

**Step 3: Create `UpdateWebhookEndpoint`**

- Route: `PATCH /booking-types/{slug}/webhooks/{webhookId}`
- Roles: `TenantAdmin`, `ApiKey`
- Scope: `webhooks:write`
- Request model: `Slug`, `WebhookId`, optional `Url`, optional `Secret`, optional `EventTypes`
- Returns 200 with updated `WebhookDto`

**Step 4: Build and verify**

**Step 5: Commit**

```
feat(api): add eventTypes to webhook create and add PATCH update endpoint
```

---

### Task 8: Application — Update `WebhookOutboxHandler` to filter by subscribed events

**Files:**

- Modify: `src/Chronith.Application/Notifications/WebhookOutboxHandler.cs`

**Step 1: After fetching webhooks and resolving `tenantEventType`, filter webhooks**

```csharp
var subscribedWebhooks = webhooks
    .Where(w => w.EventTypes.Contains(tenantEventType))
    .ToList();
```

**Step 2: Use `subscribedWebhooks` instead of `webhooks` when creating outbox entries**

**Step 3: Build and verify**

**Step 4: Commit**

```
feat(app): filter outbound webhooks by subscribed event types
```

---

### Task 9: Unit tests — WebhookOutboxHandler filtering, create command validation

**Files:**

- Modify: `tests/Chronith.Tests.Unit/Application/WebhookOutboxHandlerTests.cs`
- Modify: `tests/Chronith.Tests.Unit/Helpers/BookingTypeBuilder.cs` (if needed)

**Step 1: Update existing `WebhookOutboxHandlerTests`**

Existing `Webhook.Create(...)` calls now need event types. Update helpers.

**Step 2: Add new test cases**

- `Handle_WebhookSubscribedToEvent_CreatesOutboxEntry`
- `Handle_WebhookNotSubscribedToEvent_SkipsWebhook`
- `Handle_MultipleWebhooks_OnlySubscribedOnesGetEntries`
- `Handle_WebhookSubscribedToAllEvents_AlwaysGetsEntry`

**Step 3: Run tests**

Run: `dotnet test tests/Chronith.Tests.Unit`

**Step 4: Commit**

```
test: add unit tests for webhook event subscription filtering
```

---

### Task 10: Functional tests — Create, update, list with event types

**Files:**

- Modify: `tests/Chronith.Tests.Functional/Webhooks/WebhookEndpointsTests.cs`
- Modify: `tests/Chronith.Tests.Functional/Webhooks/WebhookAuthTests.cs`

**Step 1: Update existing create/list tests to include `eventTypes` in requests and verify in responses**

**Step 2: Add new tests**

- `CreateWebhook_WithEventTypes_Returns201WithEventTypes`
- `CreateWebhook_WithInvalidEventType_Returns400`
- `CreateWebhook_WithEmptyEventTypes_Returns400`
- `UpdateWebhook_AsAdmin_Returns200WithUpdatedFields`
- `UpdateWebhook_UpdateEventTypes_PersistsCorrectly`
- `UpdateWebhook_Anonymous_Returns401`
- `UpdateWebhook_NonAdmin_Returns403`

**Step 3: Run functional tests**

Run: `dotnet test tests/Chronith.Tests.Functional --filter "FullyQualifiedName~Webhook"`

**Step 4: Commit**

```
test: add functional tests for webhook event subscriptions and update endpoint
```

---

### Task 11: Final verification — full build and test suite

**Step 1: Build entire solution**

Run: `dotnet build Chronith.slnx`

**Step 2: Run all unit tests**

Run: `dotnet test tests/Chronith.Tests.Unit`

**Step 3: Run all functional tests (requires Docker)**

Run: `dotnet test tests/Chronith.Tests.Functional`

**Step 4: Final commit if any fixups needed**
