# Booking Reference ID Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a computed `string ReferenceId` (dashless Guid) to booking DTOs for cleaner customer-facing display, while keeping `Guid Id` unchanged for programmatic use.

**Architecture:** `ReferenceId` is derived at the mapping/handler layer via `Id.ToString("N")` — no database changes, no domain model changes. Notification template context and PDF export updated to use dashless format. Payment provider reference numbers left unchanged (external integration concern).

**Tech Stack:** .NET 10, FastEndpoints, MediatR, FluentValidation, EF Core, xUnit, FluentAssertions, NSubstitute, Next.js (dashboard)

---

### Task 1: Add `ReferenceId` to `BookingDto` + `BookingMapper`

**Files:**

- Modify: `src/Chronith.Application/DTOs/BookingDto.cs:5-24`
- Modify: `src/Chronith.Application/Mappers/BookingMapper.cs:8-39`
- Test: `tests/Chronith.Tests.Unit/Application/BookingMapperTests.cs` (create if not exists)

**Step 1: Write the failing test**

Create a unit test verifying `ReferenceId` is the dashless Guid:

```csharp
[Fact]
public void ToDto_SetsReferenceId_ToDashlessGuid()
{
    var booking = new BookingBuilder().Build();

    var dto = booking.ToDto();

    dto.ReferenceId.Should().Be(booking.Id.ToString("N"));
    dto.ReferenceId.Should().NotContain("-");
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "BookingMapperTests" -v n`
Expected: FAIL — `ReferenceId` does not exist on `BookingDto`.

**Step 3: Add `ReferenceId` to `BookingDto`**

Add `string ReferenceId` as the second positional parameter (after `Guid Id`):

```csharp
public sealed record BookingDto(
    Guid Id,
    string ReferenceId,
    Guid BookingTypeId,
    // ... rest unchanged
);
```

**Step 4: Update `BookingMapper.ToDto()` to compute `ReferenceId`**

```csharp
public static BookingDto ToDto(this Booking booking, string? paymentUrl) =>
    new(
        Id: booking.Id,
        ReferenceId: booking.Id.ToString("N"),
        BookingTypeId: booking.BookingTypeId,
        // ... rest unchanged
    );
```

**Step 5: Run test to verify it passes**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "BookingMapperTests" -v n`
Expected: PASS

**Step 6: Commit**

```bash
git add -A && git commit -m "feat(app): add ReferenceId to BookingDto and BookingMapper"
```

---

### Task 2: Add `ReferenceId` to `PublicBookingStatusDto` + query handlers

**Files:**

- Modify: `src/Chronith.Application/DTOs/PublicBookingStatusDto.cs:5-14`
- Modify: `src/Chronith.Application/Queries/Public/GetPublicBookingStatusQuery.cs:32-40`
- Modify: `src/Chronith.Application/Queries/Public/GetVerifiedBookingQuery.cs:30-38`
- Test: `tests/Chronith.Tests.Unit/Application/GetVerifiedBookingQueryHandlerTests.cs:37`
- Test: `tests/Chronith.Tests.Unit/Application/GetPublicBookingStatusQueryHandlerTests.cs` (add assertion)

**Step 1: Write the failing test**

Add assertion to existing `GetVerifiedBookingQueryHandlerTests.Handle_ValidBooking_ReturnsDto`:

```csharp
result.ReferenceId.Should().Be(BookingId.ToString("N"));
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "GetVerifiedBookingQueryHandlerTests" -v n`
Expected: FAIL — `ReferenceId` does not exist on `PublicBookingStatusDto`.

**Step 3: Add `ReferenceId` to `PublicBookingStatusDto`**

```csharp
public sealed record PublicBookingStatusDto(
    Guid Id,
    string ReferenceId,
    BookingStatus Status,
    // ... rest unchanged
);
```

**Step 4: Update both query handlers to compute `ReferenceId`**

In `GetPublicBookingStatusQueryHandler.Handle`:

```csharp
return new PublicBookingStatusDto(
    Id: booking.Id,
    ReferenceId: booking.Id.ToString("N"),
    Status: booking.Status,
    // ... rest unchanged
);
```

Same for `GetVerifiedBookingQueryHandler.Handle`.

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "GetVerifiedBookingQueryHandlerTests|GetPublicBookingStatusQueryHandlerTests" -v n`
Expected: PASS

**Step 6: Commit**

```bash
git add -A && git commit -m "feat(app): add ReferenceId to PublicBookingStatusDto and query handlers"
```

---

### Task 3: Update notification template context to use dashless format

**Files:**

- Modify: `src/Chronith.Infrastructure/Services/NotificationDispatcherService.cs:130`
- Test: `tests/Chronith.Tests.Unit/Infrastructure/NotificationDispatcherTemplateTests.cs` (add test)

**Step 1: Write the failing test**

Add a test verifying that the `booking_id` template variable is dashless:

```csharp
[Fact]
public async Task Dispatch_BookingId_TemplateVariable_IsDashless()
{
    // Set up a notification payload with a bookingId containing dashes
    // Assert that the rendered template context for booking_id has no dashes
}
```

Note: The exact test structure depends on how `NotificationDispatcherService` is tested. If integration-level testing is too complex, a simpler approach is to verify at the template rendering level. Check existing tests in `NotificationDispatcherTemplateTests.cs` for patterns.

**Step 2: Implement the change**

In `NotificationDispatcherService.cs` line 130, strip dashes:

```csharp
["booking_id"] = (TryGetStringProperty(payload, "bookingId") ?? string.Empty).Replace("-", ""),
```

**Step 3: Run tests**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "NotificationDispatcher" -v n`
Expected: PASS (update any assertions that expect dashed format)

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(infra): strip dashes from booking_id notification template variable"
```

---

### Task 4: Update PDF export to use dashless format

**Files:**

- Modify: `src/Chronith.Infrastructure/Export/PdfExportService.cs:54`

**Step 1: Update the PDF export line**

Change from:

```csharp
table.Cell().Padding(4).Text(row.Id.ToString()[..8]).FontSize(8);
```

To:

```csharp
table.Cell().Padding(4).Text(row.Id.ToString("N")[..8]).FontSize(8);
```

Note: The output is identical since the first 8 chars of both "D" and "N" format are the same hex digits (dashes appear at position 8 in "D" format). But this is explicit about intent and consistent with the rest of the changes.

**Step 2: Build to verify no compilation errors**

Run: `dotnet build src/Chronith.Infrastructure -c Release -v q`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add -A && git commit -m "refactor(infra): use explicit dashless format in PDF export"
```

---

### Task 5: Update C# SDK and dashboard

**Files:**

- Modify: `packages/sdk-csharp/src/Chronith.Client/Models/BookingDto.cs:3-17`
- Modify: `dashboard/src/app/(public)/book/[tenantSlug]/[btSlug]/success/page.tsx:143`

**Step 1: Add `ReferenceId` to C# SDK BookingDto**

Add `string? ReferenceId` (nullable for backward compatibility with older API versions):

```csharp
public sealed record BookingDto(
    Guid Id,
    string? ReferenceId,
    Guid BookingTypeId,
    // ... rest unchanged
);
```

**Step 2: Update dashboard success page**

The success page currently uses `bookingId?.slice(0, 8)`. Update to prefer `referenceId` from the API response if available, with fallback:

Note: The dashboard fetches booking data from the API. If the API returns `referenceId`, use it. Check how the booking data flows into the success page (likely via booking session store or direct API fetch).

Since the success page uses `session.confirmedBookingId` (a string from the session store, not from the API), the simplest change is to strip dashes at display time:

```tsx
<span className="font-mono text-xs text-zinc-700">
  {bookingId?.replace(/-/g, "").slice(0, 8)}
</span>
```

Note: This produces the same 8 chars since dashes don't appear in first 8 positions. But explicit for consistency.

**Step 3: Build both**

Run: `dotnet build packages/sdk-csharp -c Release -v q`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: add ReferenceId to C# SDK and update dashboard display"
```

---

### Task 6: Full build + test verification

**Step 1: Run full build**

```bash
dotnet build Chronith.slnx -c Release -v q
```

Expected: 0 errors, 0 warnings (or only pre-existing warnings)

**Step 2: Run all unit tests**

```bash
dotnet test tests/Chronith.Tests.Unit -c Release -v n
```

Expected: All tests pass

**Step 3: Commit any remaining fixes if needed**

---

## Out of Scope

- Payment provider reference numbers (PayMongo `reference_number`, Maya `requestReferenceNumber`) — external integration, changing would break existing booking references
- iCal feed UIDs (`UID:{id}@chronith`) — internal calendar identifiers, not user-facing
- OTel trace tags — internal observability, dashless format adds no value
- TypeScript SDK — auto-generated from OpenAPI spec, will pick up `referenceId` on next regeneration
