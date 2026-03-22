# API Key Scope — Wire All Endpoints Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Wire API key scope authentication (`AuthSchemes`, `Policies`) on all 71 remaining endpoints that currently use only `Roles(...)`.

**Architecture:** Each endpoint needs exactly 3 changes to `Configure()`: add `"ApiKey"` to the existing `Roles(...)` call, add `AuthSchemes("Bearer", "ApiKey")`, and add `Policies($"scope:{ApiKeyScope.XxxXxx}")`. Five new scope constants must be added to `ApiKeyScope.cs` first. Each group of endpoints gets a functional test verifying API key access works with the correct scope and fails with the wrong scope.

**Tech Stack:** .NET 10, FastEndpoints 8.x, xUnit, FluentAssertions, Testcontainers

---

## Reference — The Wired Pattern

Every endpoint `Configure()` must look like this (canonical example: `ListApiKeysEndpoint.cs`):

```csharp
Roles("TenantAdmin", "ApiKey");           // add "ApiKey" to existing roles
AuthSchemes("Bearer", "ApiKey");           // NEW line
Policies($"scope:{ApiKeyScope.TenantRead}"); // NEW line
```

**Canonical wired endpoint:** `src/Chronith.API/Endpoints/ApiKeys/ListApiKeysEndpoint.cs`
**Second example:** `src/Chronith.API/Endpoints/Bookings/DeleteBookingEndpoint.cs`

## Reference — Functional Test API Key Pattern

```csharp
// Create key via admin Bearer client
var adminClient = fixture.CreateClient("TenantAdmin");
var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
{
    description = $"key-{Guid.NewGuid():N}",
    scopes = new[] { ApiKeyScope.BookingsRead }
});
createResp.StatusCode.Should().Be(HttpStatusCode.Created);
var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

// Use key as anonymous client with X-Api-Key header
var apiKeyClient = fixture.CreateAnonymousClient();
apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);
var response = await apiKeyClient.GetAsync("/v1/...");
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

Reference test file: `tests/Chronith.Tests.Functional/Bookings/BookingAuthTests.cs:173-218`

## Reference — Skipped Endpoints (do NOT wire)

These are customer-facing only; API keys are a tenant-admin concept:

- `src/Chronith.API/Endpoints/Waitlist/JoinWaitlistEndpoint.cs` — `Roles("Customer")` only
- `src/Chronith.API/Endpoints/Waitlist/AcceptWaitlistOfferEndpoint.cs` — `Roles("Customer")` only

---

## Task 1: Add 5 Missing Scope Constants

**Files:**

- Modify: `src/Chronith.Domain/Models/ApiKeyScope.cs`

Current file ends at line 27. Add the 5 new constants after `TenantWrite` and update the `All` set.

**Step 1: Make the edit**

Change `src/Chronith.Domain/Models/ApiKeyScope.cs` to:

```csharp
// src/Chronith.Domain/Models/ApiKeyScope.cs
namespace Chronith.Domain.Models;

public static class ApiKeyScope
{
    public const string BookingsRead    = "bookings:read";
    public const string BookingsWrite   = "bookings:write";
    public const string BookingsDelete  = "bookings:delete";
    public const string BookingsConfirm = "bookings:confirm";
    public const string BookingsCancel  = "bookings:cancel";
    public const string BookingsPay     = "bookings:pay";
    public const string AvailabilityRead  = "availability:read";
    public const string StaffRead         = "staff:read";
    public const string StaffWrite        = "staff:write";
    public const string BookingTypesRead  = "booking-types:read";
    public const string BookingTypesWrite = "booking-types:write";
    public const string AnalyticsRead     = "analytics:read";
    public const string WebhooksRead      = "webhooks:read";
    public const string WebhooksWrite     = "webhooks:write";
    public const string TenantRead        = "tenant:read";
    public const string TenantWrite       = "tenant:write";
    public const string AuditRead                  = "audit:read";
    public const string NotificationsWrite          = "notifications:write";
    public const string NotificationTemplatesWrite  = "notification-templates:write";
    public const string TimeBlocksWrite             = "time-blocks:write";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        BookingsRead, BookingsWrite, BookingsDelete, BookingsConfirm, BookingsCancel,
        BookingsPay, AvailabilityRead, StaffRead, StaffWrite, BookingTypesRead,
        BookingTypesWrite, AnalyticsRead, WebhooksRead, WebhooksWrite,
        TenantRead, TenantWrite, AuditRead, NotificationsWrite,
        NotificationTemplatesWrite, TimeBlocksWrite,
    };
}
```

**Step 2: Build to verify no errors**

```bash
dotnet build Chronith.slnx -c Release --nologo -q
```

Expected: Build succeeded, 0 errors.

**Step 3: Commit**

```bash
git add src/Chronith.Domain/Models/ApiKeyScope.cs
git commit -m "feat(domain): add AuditRead, NotificationsWrite, NotificationTemplatesWrite, WebhooksRead, TimeBlocksWrite scopes"
```

---

## Task 2: Wire Bookings Endpoints (7 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Bookings/GetBookingEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/Bookings/ListBookingsEndpoint.cs` (line 26)
- `src/Chronith.API/Endpoints/Bookings/ExportBookingsEndpoint.cs` (line 22)
- `src/Chronith.API/Endpoints/Bookings/CreateBookingEndpoint.cs` (line 25)
- `src/Chronith.API/Endpoints/Bookings/RescheduleBookingEndpoint.cs` (line 21)
- `src/Chronith.API/Endpoints/Bookings/ConfirmBookingEndpoint.cs` (line 20)
- `src/Chronith.API/Endpoints/Bookings/CancelBookingEndpoint.cs` (line 20)
- `src/Chronith.API/Endpoints/Bookings/PayBookingEndpoint.cs` (line 19)

**Test file to modify:** `tests/Chronith.Tests.Functional/Bookings/BookingAuthTests.cs`

### Scope mapping

| Endpoint                    | Current Roles                                    | Add ApiKey Roles | Scope             |
| --------------------------- | ------------------------------------------------ | ---------------- | ----------------- |
| `GetBookingEndpoint`        | `TenantAdmin, TenantStaff, Customer`             | + `ApiKey`       | `BookingsRead`    |
| `ListBookingsEndpoint`      | `TenantAdmin, TenantStaff`                       | + `ApiKey`       | `BookingsRead`    |
| `ExportBookingsEndpoint`    | `TenantAdmin`                                    | + `ApiKey`       | `BookingsRead`    |
| `CreateBookingEndpoint`     | `TenantAdmin, TenantStaff, Customer`             | + `ApiKey`       | `BookingsWrite`   |
| `RescheduleBookingEndpoint` | `TenantAdmin, TenantStaff, Customer`             | + `ApiKey`       | `BookingsWrite`   |
| `ConfirmBookingEndpoint`    | `TenantAdmin, TenantStaff`                       | + `ApiKey`       | `BookingsConfirm` |
| `CancelBookingEndpoint`     | `TenantAdmin, TenantStaff, Customer`             | + `ApiKey`       | `BookingsCancel`  |
| `PayBookingEndpoint`        | `TenantAdmin, TenantStaff, TenantPaymentService` | + `ApiKey`       | `BookingsPay`     |

**Step 1: Write the failing test**

Add to `tests/Chronith.Tests.Functional/Bookings/BookingAuthTests.cs`:

```csharp
[Fact]
public async Task GetBooking_WithApiKey_WithReadScope_Returns200()
{
    var btId = await EnsureSeedAsync();
    await using var db = SeedData.CreateDbContext(fixture.Factory);
    var bookingId = await SeedData.SeedBookingAsync(db, btId,
        DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1));

    var adminClient = fixture.CreateClient("TenantAdmin");
    var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
    {
        description = $"read-scope-key-{Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.BookingsRead }
    });
    createResp.StatusCode.Should().Be(HttpStatusCode.Created);
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var response = await apiKeyClient.GetAsync($"/v1/bookings/{bookingId}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task ListBookings_WithApiKey_WithReadScope_Returns200()
{
    var btId = await EnsureSeedAsync();
    var adminClient = fixture.CreateClient("TenantAdmin");
    var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
    {
        description = $"read-scope-key-{Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.BookingsRead }
    });
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var response = await apiKeyClient.GetAsync($"/v1/booking-types/{BookingTypeSlug}/bookings");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task GetBooking_WithApiKey_WithoutReadScope_Returns403()
{
    await EnsureSeedAsync();
    var adminClient = fixture.CreateClient("TenantAdmin");
    var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
    {
        description = $"wrong-scope-key-{Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.BookingsWrite }
    });
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var response = await apiKeyClient.GetAsync($"/v1/bookings/{Guid.NewGuid()}");
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/Chronith.Tests.Functional --filter "FullyQualifiedName~BookingAuthTests.GetBooking_WithApiKey" --no-build 2>&1 | tail -20
```

Expected: FAIL — endpoint returns 403 (not yet wired).

**Step 3: Wire GetBookingEndpoint**

In `src/Chronith.API/Endpoints/Bookings/GetBookingEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer");
```

With:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsRead}");
```

Add `using Chronith.Domain.Models;` if not already present.

**Step 4: Wire ListBookingsEndpoint**

In `src/Chronith.API/Endpoints/Bookings/ListBookingsEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin", "TenantStaff");
```

With:

```csharp
Roles("TenantAdmin", "TenantStaff", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsRead}");
```

**Step 5: Wire ExportBookingsEndpoint**

In `src/Chronith.API/Endpoints/Bookings/ExportBookingsEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin");
```

With:

```csharp
Roles("TenantAdmin", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsRead}");
```

**Step 6: Wire CreateBookingEndpoint**

In `src/Chronith.API/Endpoints/Bookings/CreateBookingEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer");
```

With:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsWrite}");
```

**Step 7: Wire RescheduleBookingEndpoint**

In `src/Chronith.API/Endpoints/Bookings/RescheduleBookingEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer");
```

With:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsWrite}");
```

**Step 8: Wire ConfirmBookingEndpoint**

In `src/Chronith.API/Endpoints/Bookings/ConfirmBookingEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin", "TenantStaff");
```

With:

```csharp
Roles("TenantAdmin", "TenantStaff", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsConfirm}");
```

**Step 9: Wire CancelBookingEndpoint**

In `src/Chronith.API/Endpoints/Bookings/CancelBookingEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer");
```

With:

```csharp
Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsCancel}");
```

**Step 10: Wire PayBookingEndpoint**

In `src/Chronith.API/Endpoints/Bookings/PayBookingEndpoint.cs`, replace:

```csharp
Roles("TenantAdmin", "TenantStaff", "TenantPaymentService");
```

With:

```csharp
Roles("TenantAdmin", "TenantStaff", "TenantPaymentService", "ApiKey");
AuthSchemes("Bearer", "ApiKey");
Policies($"scope:{ApiKeyScope.BookingsPay}");
```

**Step 11: Run tests**

```bash
dotnet test tests/Chronith.Tests.Functional --filter "FullyQualifiedName~BookingAuthTests" --no-build 2>&1 | tail -30
```

Expected: All pass.

**Step 12: Commit**

```bash
git add src/Chronith.API/Endpoints/Bookings/ tests/Chronith.Tests.Functional/Bookings/BookingAuthTests.cs
git commit -m "feat(api): wire API key scope auth on bookings endpoints"
```

---

## Task 3: Wire Recurring Endpoints (6 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Recurring/ListRecurrenceRulesEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Recurring/GetRecurrenceRuleEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/Recurring/GetRecurrenceOccurrencesEndpoint.cs` (line 38)
- `src/Chronith.API/Endpoints/Recurring/CreateRecurrenceRuleEndpoint.cs` (line 33)
- `src/Chronith.API/Endpoints/Recurring/UpdateRecurrenceRuleEndpoint.cs` (line 32)
- `src/Chronith.API/Endpoints/Recurring/CancelRecurrenceRuleEndpoint.cs` (line 18)

**Test file to modify:** `tests/Chronith.Tests.Functional/Recurring/RecurringAuthTests.cs`

### Scope mapping

| Endpoint                           | Current Roles                        | Add ApiKey Roles | Scope            |
| ---------------------------------- | ------------------------------------ | ---------------- | ---------------- |
| `ListRecurrenceRulesEndpoint`      | `TenantAdmin, TenantStaff`           | + `ApiKey`       | `BookingsRead`   |
| `GetRecurrenceRuleEndpoint`        | `TenantAdmin, TenantStaff, Customer` | + `ApiKey`       | `BookingsRead`   |
| `GetRecurrenceOccurrencesEndpoint` | `TenantAdmin, TenantStaff, Customer` | + `ApiKey`       | `BookingsRead`   |
| `CreateRecurrenceRuleEndpoint`     | `TenantAdmin, TenantStaff, Customer` | + `ApiKey`       | `BookingsWrite`  |
| `UpdateRecurrenceRuleEndpoint`     | `TenantAdmin, TenantStaff`           | + `ApiKey`       | `BookingsWrite`  |
| `CancelRecurrenceRuleEndpoint`     | `TenantAdmin, TenantStaff, Customer` | + `ApiKey`       | `BookingsCancel` |

**Step 1: Write failing test** — add to `tests/Chronith.Tests.Functional/Recurring/RecurringAuthTests.cs`:

```csharp
[Fact]
public async Task ListRecurrenceRules_WithApiKey_WithReadScope_Returns200()
{
    await EnsureSeedAsync();
    var adminClient = fixture.CreateClient("TenantAdmin");
    var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
    {
        description = $"read-scope-key-{Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.BookingsRead }
    });
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var response = await apiKeyClient.GetAsync($"/v1/booking-types/{RecurringBookingTypeSlug}/recurrence-rules");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task ListRecurrenceRules_WithApiKey_WithoutReadScope_Returns403()
{
    await EnsureSeedAsync();
    var adminClient = fixture.CreateClient("TenantAdmin");
    var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
    {
        description = $"wrong-scope-key-{Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.BookingsWrite }
    });
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var response = await apiKeyClient.GetAsync($"/v1/booking-types/{RecurringBookingTypeSlug}/recurrence-rules");
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

> Note: Look at the existing test file for the correct `BookingTypeSlug` constant name and `EnsureSeedAsync()` pattern — it must match.

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/Chronith.Tests.Functional --filter "FullyQualifiedName~RecurringAuthTests.ListRecurrenceRules_WithApiKey" --no-build 2>&1 | tail -20
```

**Step 3: Wire all 6 endpoints** — apply the same 3-line change to each:

`ListRecurrenceRulesEndpoint.cs`: `Roles("TenantAdmin", "TenantStaff", "ApiKey")` + `AuthSchemes(...)` + `Policies($"scope:{ApiKeyScope.BookingsRead}")`

`GetRecurrenceRuleEndpoint.cs`: `Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey")` + `AuthSchemes(...)` + `Policies($"scope:{ApiKeyScope.BookingsRead}")`

`GetRecurrenceOccurrencesEndpoint.cs`: `Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey")` + `AuthSchemes(...)` + `Policies($"scope:{ApiKeyScope.BookingsRead}")`

`CreateRecurrenceRuleEndpoint.cs`: `Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey")` + `AuthSchemes(...)` + `Policies($"scope:{ApiKeyScope.BookingsWrite}")`

`UpdateRecurrenceRuleEndpoint.cs`: `Roles("TenantAdmin", "TenantStaff", "ApiKey")` + `AuthSchemes(...)` + `Policies($"scope:{ApiKeyScope.BookingsWrite}")`

`CancelRecurrenceRuleEndpoint.cs`: `Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey")` + `AuthSchemes(...)` + `Policies($"scope:{ApiKeyScope.BookingsCancel}")`

**Step 4: Run tests**

```bash
dotnet test tests/Chronith.Tests.Functional --filter "FullyQualifiedName~RecurringAuthTests" --no-build 2>&1 | tail -30
```

**Step 5: Commit**

```bash
git add src/Chronith.API/Endpoints/Recurring/ tests/Chronith.Tests.Functional/Recurring/RecurringAuthTests.cs
git commit -m "feat(api): wire API key scope auth on recurrence endpoints"
```

---

## Task 4: Wire Waitlist Endpoints (2 endpoints — skip Customer-only)

**Files to modify:**

- `src/Chronith.API/Endpoints/Waitlist/ListWaitlistEndpoint.cs` (line 25)
- `src/Chronith.API/Endpoints/Waitlist/RemoveFromWaitlistEndpoint.cs` (line 18)

**DO NOT touch:**

- `src/Chronith.API/Endpoints/Waitlist/JoinWaitlistEndpoint.cs` — `Roles("Customer")` only
- `src/Chronith.API/Endpoints/Waitlist/AcceptWaitlistOfferEndpoint.cs` — `Roles("Customer")` only

**Test file:** Check if `tests/Chronith.Tests.Functional/Waitlist/` exists; if not, check if waitlist tests live elsewhere. Add API key tests inline.

### Scope mapping

| Endpoint                     | Current Roles              | New Roles  | Scope            |
| ---------------------------- | -------------------------- | ---------- | ---------------- |
| `ListWaitlistEndpoint`       | `TenantAdmin, TenantStaff` | + `ApiKey` | `BookingsRead`   |
| `RemoveFromWaitlistEndpoint` | `Customer, TenantAdmin`    | + `ApiKey` | `BookingsCancel` |

**Step 1: Check for existing waitlist test file**

```bash
find tests/Chronith.Tests.Functional -name "*Waitlist*" -o -name "*waitlist*"
```

Add API key tests to whichever file exists (or create `tests/Chronith.Tests.Functional/Waitlist/WaitlistAuthTests.cs` following the standard pattern).

**Step 2–4:** Follow same failing test → implement → run → commit pattern.

```bash
git add src/Chronith.API/Endpoints/Waitlist/ tests/Chronith.Tests.Functional/Waitlist/
git commit -m "feat(api): wire API key scope auth on waitlist endpoints"
```

---

## Task 5: Wire Availability Endpoint (1 endpoint)

**File to modify:** `src/Chronith.API/Endpoints/Availability/GetAvailabilityEndpoint.cs` (line 25)

Current: `Roles("TenantAdmin", "TenantStaff", "Customer")`
Change to: `Roles("TenantAdmin", "TenantStaff", "Customer", "ApiKey")` + `AuthSchemes(...)` + `Policies($"scope:{ApiKeyScope.AvailabilityRead}")`

**Test file to modify:** `tests/Chronith.Tests.Functional/Availability/AvailabilityAuthTests.cs`

Add test:

```csharp
[Fact]
public async Task GetAvailability_WithApiKey_WithAvailabilityReadScope_Returns200()
{
    await EnsureSeedAsync();
    var adminClient = fixture.CreateClient("TenantAdmin");
    var createResp = await adminClient.PostAsJsonAsync("/v1/tenant/api-keys", new
    {
        description = $"avail-scope-key-{Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.AvailabilityRead }
    });
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();
    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var response = await apiKeyClient.GetAsync($"/v1/booking-types/{AvailabilityBookingTypeSlug}/availability?date={DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))}");
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

> Check existing test file for slug constant and URL format.

```bash
dotnet test tests/Chronith.Tests.Functional --filter "FullyQualifiedName~AvailabilityAuthTests" --no-build 2>&1 | tail -20
git add src/Chronith.API/Endpoints/Availability/ tests/Chronith.Tests.Functional/Availability/AvailabilityAuthTests.cs
git commit -m "feat(api): wire API key scope auth on availability endpoint"
```

---

## Task 6: Wire BookingTypes Endpoints (5 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/BookingTypes/ListBookingTypesEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/BookingTypes/GetBookingTypeEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/BookingTypes/CreateBookingTypeEndpoint.cs` (line 32)
- `src/Chronith.API/Endpoints/BookingTypes/UpdateBookingTypeEndpoint.cs` (line 35)
- `src/Chronith.API/Endpoints/BookingTypes/DeleteBookingTypeEndpoint.cs` (line 18)

**Test file:** `tests/Chronith.Tests.Functional/BookingTypes/BookingTypeAuthTests.cs`

### Scope mapping

| Endpoint                    | Current Roles                        | New Roles  | Scope               |
| --------------------------- | ------------------------------------ | ---------- | ------------------- |
| `ListBookingTypesEndpoint`  | `TenantAdmin, TenantStaff, Customer` | + `ApiKey` | `BookingTypesRead`  |
| `GetBookingTypeEndpoint`    | `TenantAdmin, TenantStaff, Customer` | + `ApiKey` | `BookingTypesRead`  |
| `CreateBookingTypeEndpoint` | `TenantAdmin`                        | + `ApiKey` | `BookingTypesWrite` |
| `UpdateBookingTypeEndpoint` | `TenantAdmin`                        | + `ApiKey` | `BookingTypesWrite` |
| `DeleteBookingTypeEndpoint` | `TenantAdmin`                        | + `ApiKey` | `BookingTypesWrite` |

Add two API key tests (correct scope → 200/204, wrong scope → 403), wire all 5, run, commit:

```bash
git add src/Chronith.API/Endpoints/BookingTypes/ tests/Chronith.Tests.Functional/BookingTypes/BookingTypeAuthTests.cs
git commit -m "feat(api): wire API key scope auth on booking-type endpoints"
```

---

## Task 7: Wire Staff Endpoints (7 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Staff/ListStaffEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Staff/GetStaffEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/Staff/GetStaffAvailabilityEndpoint.cs` (line 27)
- `src/Chronith.API/Endpoints/Staff/CreateStaffEndpoint.cs` (line 23)
- `src/Chronith.API/Endpoints/Staff/UpdateStaffEndpoint.cs` (line 25)
- `src/Chronith.API/Endpoints/Staff/DeleteStaffEndpoint.cs` (line 18)
- `src/Chronith.API/Endpoints/Staff/AssignStaffToBookingEndpoint.cs` (line 23)

**Test file:** `tests/Chronith.Tests.Functional/Staff/StaffAuthTests.cs`

### Scope mapping

| Endpoint                       | Current Roles                        | New Roles  | Scope        |
| ------------------------------ | ------------------------------------ | ---------- | ------------ |
| `ListStaffEndpoint`            | `TenantAdmin, TenantStaff`           | + `ApiKey` | `StaffRead`  |
| `GetStaffEndpoint`             | `TenantAdmin, TenantStaff`           | + `ApiKey` | `StaffRead`  |
| `GetStaffAvailabilityEndpoint` | `TenantAdmin, TenantStaff, Customer` | + `ApiKey` | `StaffRead`  |
| `CreateStaffEndpoint`          | `TenantAdmin`                        | + `ApiKey` | `StaffWrite` |
| `UpdateStaffEndpoint`          | `TenantAdmin`                        | + `ApiKey` | `StaffWrite` |
| `DeleteStaffEndpoint`          | `TenantAdmin`                        | + `ApiKey` | `StaffWrite` |
| `AssignStaffToBookingEndpoint` | `TenantAdmin, TenantStaff`           | + `ApiKey` | `StaffWrite` |

```bash
git add src/Chronith.API/Endpoints/Staff/ tests/Chronith.Tests.Functional/Staff/StaffAuthTests.cs
git commit -m "feat(api): wire API key scope auth on staff endpoints"
```

---

## Task 8: Wire TimeBlocks Endpoints (3 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/TimeBlocks/ListTimeBlocksEndpoint.cs` (line 29)
- `src/Chronith.API/Endpoints/TimeBlocks/CreateTimeBlockEndpoint.cs` (line 23)
- `src/Chronith.API/Endpoints/TimeBlocks/DeleteTimeBlockEndpoint.cs` (line 18)

**Test file:** Check for `tests/Chronith.Tests.Functional/TimeBlocks/` — add or create.

### Scope mapping

| Endpoint                  | Current Roles              | New Roles  | Scope              |
| ------------------------- | -------------------------- | ---------- | ------------------ |
| `ListTimeBlocksEndpoint`  | `TenantAdmin, TenantStaff` | + `ApiKey` | `AvailabilityRead` |
| `CreateTimeBlockEndpoint` | `TenantAdmin`              | + `ApiKey` | `TimeBlocksWrite`  |
| `DeleteTimeBlockEndpoint` | `TenantAdmin`              | + `ApiKey` | `TimeBlocksWrite`  |

```bash
git add src/Chronith.API/Endpoints/TimeBlocks/ tests/Chronith.Tests.Functional/TimeBlocks/
git commit -m "feat(api): wire API key scope auth on time-block endpoints"
```

---

## Task 9: Wire Analytics Endpoints (4 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Analytics/GetBookingAnalyticsEndpoint.cs` (line 26)
- `src/Chronith.API/Endpoints/Analytics/GetRevenueAnalyticsEndpoint.cs` (line 26)
- `src/Chronith.API/Endpoints/Analytics/GetUtilizationAnalyticsEndpoint.cs` (line 23)
- `src/Chronith.API/Endpoints/Analytics/ExportAnalyticsEndpoint.cs` (line 20)

**Test file:** `tests/Chronith.Tests.Functional/Analytics/AnalyticsAuthTests.cs`

All 4: `Roles("TenantAdmin", "ApiKey")` + `AuthSchemes("Bearer", "ApiKey")` + `Policies($"scope:{ApiKeyScope.AnalyticsRead}")`

```bash
git add src/Chronith.API/Endpoints/Analytics/ tests/Chronith.Tests.Functional/Analytics/AnalyticsAuthTests.cs
git commit -m "feat(api): wire API key scope auth on analytics endpoints"
```

---

## Task 10: Wire Webhooks Endpoints (5 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Webhooks/ListWebhooksEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/Webhooks/ListWebhookDeliveriesEndpoint.cs` (line 21)
- `src/Chronith.API/Endpoints/Webhooks/CreateWebhookEndpoint.cs` (line 24)
- `src/Chronith.API/Endpoints/Webhooks/DeleteWebhookEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/Webhooks/RetryWebhookDeliveryEndpoint.cs` (line 13)

**Test file:** `tests/Chronith.Tests.Functional/Webhooks/WebhookAuthTests.cs`

### Scope mapping

| Endpoint                        | Current Roles              | New Roles  | Scope           |
| ------------------------------- | -------------------------- | ---------- | --------------- |
| `ListWebhooksEndpoint`          | `TenantAdmin`              | + `ApiKey` | `WebhooksRead`  |
| `ListWebhookDeliveriesEndpoint` | `TenantAdmin, TenantStaff` | + `ApiKey` | `WebhooksRead`  |
| `CreateWebhookEndpoint`         | `TenantAdmin`              | + `ApiKey` | `WebhooksWrite` |
| `DeleteWebhookEndpoint`         | `TenantAdmin`              | + `ApiKey` | `WebhooksWrite` |
| `RetryWebhookDeliveryEndpoint`  | `TenantAdmin`              | + `ApiKey` | `WebhooksWrite` |

```bash
git add src/Chronith.API/Endpoints/Webhooks/ tests/Chronith.Tests.Functional/Webhooks/WebhookAuthTests.cs
git commit -m "feat(api): wire API key scope auth on webhook endpoints"
```

---

## Task 11: Wire Audit Endpoints (3 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Audit/GetAuditEntriesEndpoint.cs` (line 41)
- `src/Chronith.API/Endpoints/Audit/GetAuditEntryByIdEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/Audit/ExportAuditEndpoint.cs` (line 18)

**Test file:** `tests/Chronith.Tests.Functional/Audit/AuditAuthTests.cs`

All 3: `Roles("TenantAdmin", "ApiKey")` + `AuthSchemes("Bearer", "ApiKey")` + `Policies($"scope:{ApiKeyScope.AuditRead}")`

```bash
git add src/Chronith.API/Endpoints/Audit/ tests/Chronith.Tests.Functional/Audit/AuditAuthTests.cs
git commit -m "feat(api): wire API key scope auth on audit endpoints"
```

---

## Task 12: Wire Tenant Endpoints (17 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Tenant/GetTenantEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Tenant/GetTenantSettingsEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Tenant/GetTenantMetricsEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Tenant/GetTenantAuthConfigEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Tenant/GetUsageEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Tenant/GetSubscriptionEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Tenant/TenantPaymentConfigEndpoints.cs` (6 endpoints in one file)
- `src/Chronith.API/Endpoints/Tenant/UpdateTenantSettingsEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Tenant/TenantAuthConfigEndpoint.cs` (line 23)
- `src/Chronith.API/Endpoints/Tenant/SubscribeEndpoint.cs` (line 20)
- `src/Chronith.API/Endpoints/Tenant/ChangePlanEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/Tenant/CancelSubscriptionEndpoint.cs` (line 18)

**Test files:** `tests/Chronith.Tests.Functional/Tenant/TenantAuthTests.cs`, `tests/Chronith.Tests.Functional/Tenant/TenantSettingsAuthTests.cs`, `tests/Chronith.Tests.Functional/Subscriptions/SubscriptionAuthTests.cs`, `tests/Chronith.Tests.Functional/TenantPaymentConfig/TenantPaymentConfigAuthTests.cs`

### Scope mapping

| Endpoint(s)                                                                                                                                                                                                                                                                                | Current Roles              | New Roles  | Scope         |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------- | ---------- | ------------- |
| `GetTenantEndpoint`, `GetTenantSettingsEndpoint`, `GetTenantAuthConfigEndpoint`, `GetSubscriptionEndpoint`, `ListTenantPaymentConfigs`                                                                                                                                                     | `TenantAdmin`              | + `ApiKey` | `TenantRead`  |
| `GetTenantMetricsEndpoint`, `GetUsageEndpoint`                                                                                                                                                                                                                                             | `TenantAdmin, TenantStaff` | + `ApiKey` | `TenantRead`  |
| `UpdateTenantSettingsEndpoint`, `TenantAuthConfigEndpoint`, `SubscribeEndpoint`, `ChangePlanEndpoint`, `CancelSubscriptionEndpoint`, `CreateTenantPaymentConfig`, `UpdateTenantPaymentConfig`, `DeleteTenantPaymentConfig`, `ActivateTenantPaymentConfig`, `DeactivateTenantPaymentConfig` | `TenantAdmin`              | + `ApiKey` | `TenantWrite` |

> **Note on `TenantPaymentConfigEndpoints.cs`:** This single file contains 6 endpoint classes. Apply the 3-line change inside each class's `Configure()` method individually.

Add two tests (one per scope: `TenantRead` → 200, `TenantWrite` → 200/204), run, commit:

```bash
git add src/Chronith.API/Endpoints/Tenant/ tests/Chronith.Tests.Functional/Tenant/ tests/Chronith.Tests.Functional/Subscriptions/ tests/Chronith.Tests.Functional/TenantPaymentConfig/
git commit -m "feat(api): wire API key scope auth on tenant endpoints"
```

---

## Task 13: Wire Notifications Endpoints (3 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/Notifications/ListNotificationConfigsEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/Notifications/UpdateNotificationConfigEndpoint.cs` (line 20)
- `src/Chronith.API/Endpoints/Notifications/DisableNotificationChannelEndpoint.cs` (line 18)

**Test file:** `tests/Chronith.Tests.Functional/Notifications/NotificationAuthTests.cs`

All 3: `Roles("TenantAdmin", "ApiKey")` + `AuthSchemes("Bearer", "ApiKey")` + `Policies($"scope:{ApiKeyScope.NotificationsWrite}")`

```bash
git add src/Chronith.API/Endpoints/Notifications/ tests/Chronith.Tests.Functional/Notifications/NotificationAuthTests.cs
git commit -m "feat(api): wire API key scope auth on notification endpoints"
```

---

## Task 14: Wire NotificationTemplates Endpoints (5 endpoints)

**Files to modify:**

- `src/Chronith.API/Endpoints/NotificationTemplates/GetNotificationTemplatesEndpoint.cs` (line 14)
- `src/Chronith.API/Endpoints/NotificationTemplates/GetNotificationTemplateByIdEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/NotificationTemplates/UpdateNotificationTemplateEndpoint.cs` (line 25)
- `src/Chronith.API/Endpoints/NotificationTemplates/ResetNotificationTemplateEndpoint.cs` (line 19)
- `src/Chronith.API/Endpoints/NotificationTemplates/PreviewNotificationTemplateEndpoint.cs` (line 26)

**Test file:** `tests/Chronith.Tests.Functional/NotificationTemplates/NotificationTemplateAuthTests.cs`

All 5: `Roles("TenantAdmin", "ApiKey")` + `AuthSchemes("Bearer", "ApiKey")` + `Policies($"scope:{ApiKeyScope.NotificationTemplatesWrite}")`

```bash
git add src/Chronith.API/Endpoints/NotificationTemplates/ tests/Chronith.Tests.Functional/NotificationTemplates/NotificationTemplateAuthTests.cs
git commit -m "feat(api): wire API key scope auth on notification-template endpoints"
```

---

## Task 15: Final Build + Full Test Suite

**Step 1: Build**

```bash
dotnet build Chronith.slnx -c Release --nologo -q
```

Expected: 0 errors, 0 warnings.

**Step 2: Run unit tests**

```bash
dotnet test tests/Chronith.Tests.Unit --no-build -c Release 2>&1 | tail -10
```

Expected: All pass.

**Step 3: Run integration tests**

```bash
dotnet test tests/Chronith.Tests.Integration --no-build -c Release 2>&1 | tail -10
```

Expected: All pass.

**Step 4: Run functional tests**

```bash
dotnet test tests/Chronith.Tests.Functional --no-build -c Release 2>&1 | tail -20
```

Expected: All pass.

---

## Task 16: Open Pull Request

**Step 1: Push branch**

```bash
git push origin feat/api-key-scope-all-endpoints
```

**Step 2: Create PR**

```bash
gh pr create \
  --title "feat(api): wire API key scope auth on all remaining endpoints" \
  --body "$(cat <<'EOF'
## Summary

- Adds 5 new scope constants to `ApiKeyScope`: `AuditRead`, `NotificationsWrite`, `NotificationTemplatesWrite`, `WebhooksRead`, `TimeBlocksWrite`
- Wires `AuthSchemes("Bearer", "ApiKey")` + `Policies($"scope:...")` on all 71 remaining endpoints across 13 groups
- Skips `JoinWaitlistEndpoint` and `AcceptWaitlistOfferEndpoint` (Customer-only, API keys are a tenant-admin concept)
- Adds functional API key auth tests for each endpoint group

## Test coverage

Each group has at minimum:
- ✅ API key with correct scope → 200/201/204
- ✅ API key with wrong scope → 403

## Checklist

- [x] New scope constants in `ApiKeyScope.cs` + `All` set updated
- [x] All 71 endpoints wired
- [x] Functional tests for each group
- [x] Full test suite passes
EOF
)" \
  --base main \
  --head feat/api-key-scope-all-endpoints
```
