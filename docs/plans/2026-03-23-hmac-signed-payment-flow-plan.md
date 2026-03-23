# HMAC-Signed Payment Flow — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace immediate checkout creation at booking time with a two-step HMAC-signed payment flow: (1) booking creation returns a signed payment URL, (2) checkout sessions are created on-demand when the customer picks a provider.

**Architecture:** HMAC-SHA256 signs `booking-access.{bookingId}.{tenantSlug}.{expires}` using the existing `Security:HmacKey` with domain separation. Two new public endpoints authenticate via HMAC query params. Both `CreateBookingHandler` and `PublicCreateBookingHandler` are modified to skip immediate checkout and return a payment URL instead.

**Tech Stack:** .NET 10, FastEndpoints, MediatR, FluentValidation, HMAC-SHA256, xUnit, FluentAssertions, NSubstitute

**Design doc:** `docs/plans/2026-03-23-hmac-signed-payment-flow-design.md`

---

## Task 1: IBookingUrlSigner Interface + HmacBookingUrlSigner + Unit Tests + DI

**Files:**

- Create: `src/Chronith.Application/Interfaces/IBookingUrlSigner.cs`
- Create: `src/Chronith.Application/Options/PaymentPageOptions.cs`
- Create: `src/Chronith.Infrastructure/Security/HmacBookingUrlSigner.cs`
- Create: `tests/Chronith.Tests.Unit/Infrastructure/Security/HmacBookingUrlSignerTests.cs`
- Modify: `src/Chronith.Infrastructure/DependencyInjection.cs:146-152`
- Modify: `src/Chronith.API/appsettings.json` (add PaymentPage section)

### Step 1: Write failing tests

```csharp
// tests/Chronith.Tests.Unit/Infrastructure/Security/HmacBookingUrlSignerTests.cs
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public sealed class HmacBookingUrlSignerTests
{
    // 32 bytes of 0x01 — valid HMAC key
    private static readonly string ValidKey =
        Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());

    private static IBookingUrlSigner CreateSigner(int lifetimeSeconds = 3600)
    {
        var hmacOptions = Options.Create(new BlindIndexOptions { HmacKey = ValidKey });
        var pageOptions = Options.Create(new PaymentPageOptions
        {
            TokenLifetimeSeconds = lifetimeSeconds
        });
        return new HmacBookingUrlSigner(hmacOptions, pageOptions);
    }

    [Fact]
    public void GenerateSignedUrl_ReturnsUrlWithAllParams()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, "test-tenant");

        url.Should().StartWith("https://app.com/pay?");
        url.Should().Contain($"bookingId={bookingId}");
        url.Should().Contain("tenantSlug=test-tenant");
        url.Should().Contain("expires=");
        url.Should().Contain("sig=");
    }

    [Fact]
    public void GenerateSignedUrl_ExpiresIsInFuture()
    {
        var signer = CreateSigner();
        var url = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");

        var query = new Uri(url).Query;
        var expires = long.Parse(
            System.Web.HttpUtility.ParseQueryString(query)["expires"]!);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        expires.Should().BeGreaterThan(now);
        expires.Should().BeLessThanOrEqualTo(now + 3600 + 5); // 1h + small tolerance
    }

    [Fact]
    public void GenerateSignedUrl_SignatureIs64CharHex()
    {
        var signer = CreateSigner();
        var url = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");

        var sig = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["sig"]!;
        sig.Should().HaveLength(64);
        sig.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Validate_WithValidSignature_ReturnsTrue()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);
        var sig = qs["sig"]!;

        signer.Validate(bookingId, tenantSlug, expires, sig).Should().BeTrue();
    }

    [Fact]
    public void Validate_WithExpiredToken_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        // Generate a URL, then extract sig — but pretend expires was in the past
        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var sig = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["sig"]!;
        var pastExpires = DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds();

        // sig was computed for a future expires, so this will fail both expiry AND sig check
        signer.Validate(bookingId, tenantSlug, pastExpires, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTamperedBookingId_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);
        var sig = qs["sig"]!;

        signer.Validate(Guid.NewGuid(), tenantSlug, expires, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTamperedTenantSlug_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, "tenant-a");
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);
        var sig = qs["sig"]!;

        signer.Validate(bookingId, "tenant-b", expires, sig).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithTamperedSignature_ReturnsFalse()
    {
        var signer = CreateSigner();
        var bookingId = Guid.NewGuid();
        var tenantSlug = "test-tenant";

        var url = signer.GenerateSignedUrl("https://app.com/pay", bookingId, tenantSlug);
        var qs = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var expires = long.Parse(qs["expires"]!);

        signer.Validate(bookingId, tenantSlug, expires, "deadbeef" + new string('0', 56))
            .Should().BeFalse();
    }

    [Fact]
    public void DifferentBookings_ProduceDifferentSignatures()
    {
        var signer = CreateSigner();
        var url1 = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");
        var url2 = signer.GenerateSignedUrl("https://app.com/pay", Guid.NewGuid(), "t");

        var sig1 = System.Web.HttpUtility.ParseQueryString(new Uri(url1).Query)["sig"]!;
        var sig2 = System.Web.HttpUtility.ParseQueryString(new Uri(url2).Query)["sig"]!;
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void Constructor_InvalidKey_Throws()
    {
        var hmacOptions = Options.Create(new BlindIndexOptions { HmacKey = "" });
        var pageOptions = Options.Create(new PaymentPageOptions());
        var act = () => new HmacBookingUrlSigner(hmacOptions, pageOptions);
        act.Should().Throw<InvalidOperationException>();
    }
}
```

### Step 2: Run tests to verify they fail

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~HmacBookingUrlSignerTests" --no-restore`
Expected: Compilation errors — types do not exist yet.

### Step 3: Create interface and options

```csharp
// src/Chronith.Application/Interfaces/IBookingUrlSigner.cs
namespace Chronith.Application.Interfaces;

/// <summary>
/// Signs and validates HMAC-authenticated URLs for booking access.
/// </summary>
public interface IBookingUrlSigner
{
    /// <summary>
    /// Generates an HMAC-signed URL by appending bookingId, tenantSlug, expires, and sig query params.
    /// </summary>
    string GenerateSignedUrl(string baseUrl, Guid bookingId, string tenantSlug);

    /// <summary>
    /// Validates an HMAC signature for booking access. Returns false if expired or tampered.
    /// </summary>
    bool Validate(Guid bookingId, string tenantSlug, long expires, string signature);
}
```

```csharp
// src/Chronith.Application/Options/PaymentPageOptions.cs
namespace Chronith.Application.Options;

public sealed class PaymentPageOptions
{
    public const string SectionName = "PaymentPage";

    /// <summary>
    /// Base URL for the payment selection page (e.g. "https://booking.example.com/pay").
    /// HMAC query parameters are appended by IBookingUrlSigner.
    /// </summary>
    public string BaseUrl { get; set; } = "https://example.com/pay";

    /// <summary>
    /// Lifetime of generated booking access tokens in seconds. Default: 3600 (1 hour).
    /// </summary>
    public int TokenLifetimeSeconds { get; set; } = 3600;
}
```

### Step 4: Implement HmacBookingUrlSigner

```csharp
// src/Chronith.Infrastructure/Security/HmacBookingUrlSigner.cs
using System.Security.Cryptography;
using System.Text;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Security;

public sealed class HmacBookingUrlSigner : IBookingUrlSigner
{
    private const string DomainPrefix = "booking-access";
    private readonly byte[] _key;
    private readonly int _lifetimeSeconds;

    public HmacBookingUrlSigner(
        IOptions<BlindIndexOptions> hmacOptions,
        IOptions<PaymentPageOptions> pageOptions)
    {
        var opts = hmacOptions.Value;
        if (string.IsNullOrWhiteSpace(opts.HmacKey))
            throw new InvalidOperationException(
                "Security:HmacKey must be set for booking URL signing.");

        try
        {
            _key = Convert.FromBase64String(opts.HmacKey);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Security:HmacKey is not valid Base64.", ex);
        }

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Security:HmacKey must be exactly 32 bytes. Got {_key.Length} bytes.");

        _lifetimeSeconds = pageOptions.Value.TokenLifetimeSeconds;
    }

    public string GenerateSignedUrl(string baseUrl, Guid bookingId, string tenantSlug)
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(_lifetimeSeconds).ToUnixTimeSeconds();
        var signature = ComputeSignature(bookingId, tenantSlug, expires);

        return $"{baseUrl}?bookingId={bookingId}&tenantSlug={tenantSlug}&expires={expires}&sig={signature}";
    }

    public bool Validate(Guid bookingId, string tenantSlug, long expires, string signature)
    {
        if (expires < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            return false;

        var expected = ComputeSignature(bookingId, tenantSlug, expires);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private string ComputeSignature(Guid bookingId, string tenantSlug, long expires)
    {
        var payload = $"{DomainPrefix}.{bookingId}.{tenantSlug}.{expires}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

### Step 5: Register in DI and add appsettings

In `DependencyInjection.cs`, add after line 152 (after `IAuditPiiRedactor`):

```csharp
services.Configure<PaymentPageOptions>(configuration.GetSection(PaymentPageOptions.SectionName));
services.AddSingleton<IBookingUrlSigner, HmacBookingUrlSigner>();
```

In `appsettings.json`, add a `PaymentPage` section (before or after the existing `Security` section):

```json
"PaymentPage": {
    "BaseUrl": "https://example.com/pay",
    "TokenLifetimeSeconds": 3600
}
```

### Step 6: Run tests

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~HmacBookingUrlSignerTests" -v n`
Expected: All 9 tests pass.

### Step 7: Commit

```
feat(infra): add HMAC booking URL signer with IBookingUrlSigner interface
```

---

## Task 2: CreateCheckoutRequest DTO Changes + Provider URL Fallback

**Files:**

- Modify: `src/Chronith.Application/DTOs/PaymentDtos.cs:5-10`
- Modify: `src/Chronith.Infrastructure/Payments/PayMongo/PayMongoProvider.cs:48-49`
- Modify: `src/Chronith.Infrastructure/Payments/MayaProvider.cs:49-51`
- Modify: `src/Chronith.Infrastructure/Payments/StubPaymentProvider.cs:13-19`

### Step 1: Write failing test

No new test file needed. The existing `PayMongoProviderTests` and `StubPaymentProviderTests` must still pass after the DTO change (backward compatibility). Verify the build compiles with new optional params.

### Step 2: Update CreateCheckoutRequest

In `PaymentDtos.cs`, change the record to:

```csharp
public sealed record CreateCheckoutRequest(
    long AmountInCentavos,
    string Currency,
    string Description,
    Guid BookingId,
    Guid TenantId,
    string? SuccessUrl = null,
    string? CancelUrl = null);
```

### Step 3: Update PayMongoProvider (lines 48-49)

Replace:

```csharp
success_url = options.Value.SuccessUrl.Replace("{bookingId}", request.BookingId.ToString()),
cancel_url = options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString()),
```

With:

```csharp
success_url = request.SuccessUrl
    ?? options.Value.SuccessUrl.Replace("{bookingId}", request.BookingId.ToString()),
cancel_url = request.CancelUrl
    ?? options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString()),
```

### Step 4: Update MayaProvider (lines 49-51)

Replace:

```csharp
success = options.Value.SuccessUrl.Replace("{bookingId}", request.BookingId.ToString()),
failure = options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString()),
cancel = options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString())
```

With:

```csharp
success = request.SuccessUrl
    ?? options.Value.SuccessUrl.Replace("{bookingId}", request.BookingId.ToString()),
failure = request.CancelUrl
    ?? options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString()),
cancel = request.CancelUrl
    ?? options.Value.FailureUrl.Replace("{bookingId}", request.BookingId.ToString())
```

### Step 5: Update StubPaymentProvider

No change needed — `StubPaymentProvider.CreateCheckoutSessionAsync` doesn't use URLs from the request. It returns a stub URL. This is fine for the stub.

### Step 6: Run tests

Run: `dotnet build Chronith.slnx && dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~PayMongo or FullyQualifiedName~Maya or FullyQualifiedName~Stub" -v n`
Expected: All pass (backward compatible — optional params default to null, providers fall back to options).

### Step 7: Commit

```
feat(app): add optional SuccessUrl/CancelUrl to CreateCheckoutRequest with provider fallback
```

---

## Task 3: BookingDto.PaymentUrl + BookingMapper Update

**Files:**

- Modify: `src/Chronith.Application/DTOs/BookingDto.cs:5-19`
- Modify: `src/Chronith.Application/Mappers/BookingMapper.cs:8-31`

### Step 1: Add PaymentUrl to BookingDto

In `BookingDto.cs`, add `string? PaymentUrl` as the last parameter:

```csharp
public sealed record BookingDto(
    Guid Id,
    Guid BookingTypeId,
    DateTimeOffset Start,
    DateTimeOffset End,
    BookingStatus Status,
    string CustomerId,
    string CustomerEmail,
    string? PaymentReference,
    long AmountInCentavos,
    string Currency,
    string? CheckoutUrl,
    Guid? StaffMemberId,
    IReadOnlyList<BookingStatusChangeDto> StatusChanges,
    string? PaymentUrl = null
);
```

### Step 2: Update BookingMapper

Add `PaymentUrl: null` explicitly to the existing `ToDto()` and add a second overload:

```csharp
public static class BookingMapper
{
    public static BookingDto ToDto(this Booking booking) =>
        booking.ToDto(paymentUrl: null);

    public static BookingDto ToDto(this Booking booking, string? paymentUrl) =>
        new(
            Id: booking.Id,
            BookingTypeId: booking.BookingTypeId,
            Start: booking.Start,
            End: booking.End,
            Status: booking.Status,
            CustomerId: booking.CustomerId,
            CustomerEmail: booking.CustomerEmail,
            PaymentReference: booking.PaymentReference,
            AmountInCentavos: booking.AmountInCentavos,
            Currency: booking.Currency,
            CheckoutUrl: booking.CheckoutUrl,
            StaffMemberId: booking.StaffMemberId,
            StatusChanges: booking.StatusChanges
                .Select(sc => new BookingStatusChangeDto(
                    sc.Id,
                    sc.FromStatus,
                    sc.ToStatus,
                    sc.ChangedById,
                    sc.ChangedByRole,
                    sc.ChangedAt))
                .ToList(),
            PaymentUrl: paymentUrl
        );
}
```

### Step 3: Run tests

Run: `dotnet build Chronith.slnx && dotnet test tests/Chronith.Tests.Unit -v n`
Expected: All pass — existing callers use `booking.ToDto()` which passes null for PaymentUrl.

### Step 4: Commit

```
feat(app): add PaymentUrl field to BookingDto with mapper overload
```

---

## Task 4: Modify CreateBookingHandler (Skip Checkout, Generate Payment URL)

**Files:**

- Modify: `src/Chronith.Application/Commands/Bookings/CreateBookingCommand.cs:46-162`
- Modify: `tests/Chronith.Tests.Unit/Application/CreateBookingHandlerTests.cs`

### Step 1: Update unit tests

The handler will no longer create checkout sessions. It will generate a payment URL instead. Update the existing tests and add new ones.

**Tests to modify:**

1. `Handle_AutomaticPaymentMode_CallsCreateCheckoutSessionAsync` → **REMOVE** (no longer calls provider)
2. `Handle_AutomaticPaymentMode_CallsUpdateAsyncAfterCheckoutSessionCreation` → **REMOVE**
3. `Handle_AutomaticPaymentMode_ReturnedDtoContainsPaymentReference` → **CHANGE** to assert `PaymentReference` is null (no checkout yet)
4. `Handle_AutomaticPaymentMode_ReturnedDtoContainsCheckoutUrl` → **CHANGE** to assert `CheckoutUrl` is null
5. `Handle_AutomaticPaymentMode_PassesCorrectCheckoutRequest` → **REMOVE**
6. `Handle_AutomaticPaymentMode_RecordsPaymentProcessedMetric` → **REMOVE** (no payment processing at creation)
7. `Handle_WhenResolverReturnsNull_SkipsCheckoutAndStaysAtPendingPayment` → **REMOVE** (no resolver call at creation)

**New tests to add:**

```csharp
[Fact]
public async Task Handle_AutomaticPaidBooking_ReturnsPaymentUrl()
{
    var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
    var (handler, _, _, _, _) = Build(bookingType);

    var result = await handler.Handle(MakeCommand(), CancellationToken.None);

    result.PaymentUrl.Should().NotBeNullOrEmpty();
    result.PaymentUrl.Should().Contain("bookingId=");
    result.PaymentUrl.Should().Contain("tenantSlug=");
    result.PaymentUrl.Should().Contain("sig=");
}

[Fact]
public async Task Handle_AutomaticPaidBooking_DoesNotCreateCheckoutSession()
{
    var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub");
    var (handler, _, _, provider, _) = Build(bookingType);

    await handler.Handle(MakeCommand(), CancellationToken.None);

    await provider.DidNotReceive()
        .CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>());
}

[Fact]
public async Task Handle_FreeBooking_PaymentUrlIsNull()
{
    var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Automatic, "Stub", priceInCentavos: 0);
    var (handler, _, _, _, _) = Build(bookingType);

    var result = await handler.Handle(MakeCommand(), CancellationToken.None);

    result.PaymentUrl.Should().BeNull();
}

[Fact]
public async Task Handle_ManualMode_PaymentUrlIsNull()
{
    var bookingType = BuildTimeSlotWithAllDayWindows(PaymentMode.Manual);
    var (handler, _, _, _, _) = Build(bookingType);

    var result = await handler.Handle(MakeCommand(), CancellationToken.None);

    result.PaymentUrl.Should().BeNull();
}
```

**Update the `Build()` method** to inject `IBookingUrlSigner` and `IOptions<PaymentPageOptions>`:

```csharp
var signer = Substitute.For<IBookingUrlSigner>();
signer.GenerateSignedUrl(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
    .Returns(ci => $"https://test.com/pay?bookingId={ci.ArgAt<Guid>(1)}&tenantSlug={ci.ArgAt<string>(2)}&expires=999&sig=abc");

var pageOptions = Options.Create(new PaymentPageOptions { BaseUrl = "https://test.com/pay" });
```

And update the handler constructor call:

```csharp
var handler = new CreateBookingHandler(
    tenantCtx,
    bookingTypeRepo,
    bookingRepo,
    tenantRepo,
    unitOfWork,
    publisher,
    tenantPaymentProviderResolver,  // keep — used by on-demand checkout later
    metrics,
    signer,        // NEW
    pageOptions);  // NEW
```

### Step 2: Run tests — should fail

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~CreateBookingHandlerTests" -v n`
Expected: Compilation errors or test failures.

### Step 3: Modify CreateBookingHandler

In `CreateBookingCommand.cs`, update the handler:

1. **Add new dependencies** to primary constructor: `IBookingUrlSigner signer`, `IOptions<PaymentPageOptions> pageOptions`
2. **Remove** the checkout session creation block (lines 115-145)
3. **Add** payment URL generation:

Replace lines 115-145 with:

```csharp
// For Automatic payment mode with a non-free booking, generate HMAC-signed payment URL
string? paymentUrl = null;
if (bookingType.PaymentMode == PaymentMode.Automatic && bookingType.PriceInCentavos > 0)
{
    paymentUrl = signer.GenerateSignedUrl(
        pageOptions.Value.BaseUrl, booking.Id, tenant.Slug);
}
```

4. **Update the return** from `return booking.ToDto();` to `return booking.ToDto(paymentUrl);`
5. **Keep** `ITenantPaymentProviderResolver` in constructor (still needed for future reference, or remove if unused). Actually, remove it — the handler no longer resolves providers. The on-demand checkout uses a separate command.

Wait — we should keep `ITenantPaymentProviderResolver` out of this handler now. Remove it from the constructor. Also remove the `IBookingMetrics.RecordPaymentProcessed` call (no payment at creation time).

### Step 4: Run tests

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~CreateBookingHandlerTests" -v n`
Expected: All pass.

### Step 5: Commit

```
feat(app): replace immediate checkout with HMAC payment URL in CreateBookingHandler
```

---

## Task 5: Modify PublicCreateBookingHandler (Migrate Factory → Resolver, Generate Payment URL)

**Files:**

- Modify: `src/Chronith.Application/Commands/Public/PublicCreateBookingCommand.cs:39-123`
- Create: `tests/Chronith.Tests.Unit/Application/PublicCreateBookingHandlerTests.cs`

### Step 1: Write failing tests

```csharp
// tests/Chronith.Tests.Unit/Application/PublicCreateBookingHandlerTests.cs
using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class PublicCreateBookingHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Slug = "test-slot";
    private static readonly DateTimeOffset FixedStart = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private static TimeSlotBookingType BuildTimeSlot(
        PaymentMode paymentMode = PaymentMode.Manual,
        string? paymentProvider = null,
        long priceInCentavos = 50000)
    {
        var allDayWindows = Enum.GetValues<DayOfWeek>()
            .Select(d => new TimeSlotWindow(d, new TimeOnly(0, 0), new TimeOnly(23, 0)))
            .ToList();

        return BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: allDayWindows,
            tenantId: TenantId,
            paymentMode: paymentMode,
            paymentProvider: paymentProvider,
            priceInCentavos: priceInCentavos);
    }

    private static (PublicCreateBookingHandler Handler, IBookingUrlSigner Signer) Build(
        BookingType bookingType)
    {
        var tenant = Tenant.Create("test-tenant", "Test Tenant", "UTC");

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        bookingTypeRepo.GetBySlugAsync(TenantId, Slug, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo.CountConflictsAsync(
            bookingType.Id, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<IReadOnlyList<BookingStatus>>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(tenant);

        var tx = Substitute.For<IUnitOfWorkTransaction>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(tx);

        var publisher = Substitute.For<IPublisher>();

        var signer = Substitute.For<IBookingUrlSigner>();
        signer.GenerateSignedUrl(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(ci => $"https://test.com/pay?bookingId={ci.ArgAt<Guid>(1)}&tenantSlug={ci.ArgAt<string>(2)}&sig=test");

        var pageOptions = Options.Create(new PaymentPageOptions { BaseUrl = "https://test.com/pay" });

        var handler = new PublicCreateBookingHandler(
            bookingTypeRepo,
            bookingRepo,
            tenantRepo,
            unitOfWork,
            publisher,
            signer,
            pageOptions);

        return (handler, signer);
    }

    private static PublicCreateBookingCommand MakeCommand() => new()
    {
        TenantId = TenantId,
        BookingTypeSlug = Slug,
        StartTime = FixedStart,
        CustomerEmail = "customer@example.com",
        CustomerId = "cust-1"
    };

    [Fact]
    public async Task Handle_AutomaticPaidBooking_ReturnsPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().NotBeNullOrEmpty();
        result.PaymentUrl.Should().Contain("bookingId=");
    }

    [Fact]
    public async Task Handle_FreeBooking_NoPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "Stub", priceInCentavos: 0);
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().BeNull();
        result.Status.Should().Be(BookingStatus.PendingVerification);
    }

    [Fact]
    public async Task Handle_ManualMode_NoPaymentUrl()
    {
        var bt = BuildTimeSlot(PaymentMode.Manual);
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.PaymentUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_StatusIsPendingPayment()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.Status.Should().Be(BookingStatus.PendingPayment);
    }

    [Fact]
    public async Task Handle_AutomaticPaidBooking_CheckoutUrlIsNull()
    {
        var bt = BuildTimeSlot(PaymentMode.Automatic, "PayMongo");
        var (handler, _) = Build(bt);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull("checkout is created on-demand, not at booking creation");
    }
}
```

### Step 2: Run tests — should fail

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~PublicCreateBookingHandlerTests" -v n`
Expected: Compilation errors.

### Step 3: Modify PublicCreateBookingHandler

1. **Replace** `IPaymentProviderFactory paymentProviderFactory` with `IBookingUrlSigner signer` and `IOptions<PaymentPageOptions> pageOptions` in constructor
2. **Remove** the checkout session creation block (lines 91-106)
3. **Add** payment URL generation (same pattern as Task 4)
4. **Update** return to use `booking.ToDto(paymentUrl)`

The handler should now look like:

```csharp
public sealed class PublicCreateBookingHandler(
    IBookingTypeRepository bookingTypeRepo,
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IBookingUrlSigner signer,
    IOptions<PaymentPageOptions> pageOptions)
    : IRequestHandler<PublicCreateBookingCommand, BookingDto>
{
    // ... Handle method ...
    // Remove lines 91-106 (checkout creation)
    // Add payment URL generation:
    string? paymentUrl = null;
    if (bookingType.PaymentMode == PaymentMode.Automatic && bookingType.PriceInCentavos > 0)
    {
        paymentUrl = signer.GenerateSignedUrl(
            pageOptions.Value.BaseUrl, booking.Id, tenant.Slug);
    }
    // Change return to: return booking.ToDto(paymentUrl);
}
```

### Step 4: Run tests

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~PublicCreateBookingHandlerTests" -v n`
Expected: All pass.

### Step 5: Commit

```
feat(app): replace IPaymentProviderFactory with HMAC payment URL in PublicCreateBookingHandler
```

---

## Task 6: CreatePublicCheckoutCommand + Handler + Unit Tests

This is the new on-demand checkout creation endpoint's command/handler.

**Files:**

- Create: `src/Chronith.Application/Commands/Public/CreatePublicCheckoutCommand.cs`
- Create: `tests/Chronith.Tests.Unit/Application/CreatePublicCheckoutCommandHandlerTests.cs`

### Step 1: Write failing tests

```csharp
// tests/Chronith.Tests.Unit/Application/CreatePublicCheckoutCommandHandlerTests.cs
using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CreatePublicCheckoutCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingId = Guid.NewGuid();

    private static (CreatePublicCheckoutHandler Handler, IPaymentProvider Provider, IBookingRepository BookingRepo)
        Build(Booking? booking = null, IPaymentProvider? provider = null)
    {
        var bookingRepo = Substitute.For<IBookingRepository>();
        var resolvedBooking = booking ?? new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50_000)
            .Build();
        bookingRepo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(resolvedBooking);

        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetBySlugAsync("test-tenant", Arg.Any<CancellationToken>())
            .Returns(Tenant.Create("test-tenant", "Test", "UTC"));

        var mockProvider = provider ?? Substitute.For<IPaymentProvider>();
        mockProvider.CreateCheckoutSessionAsync(Arg.Any<CreateCheckoutRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateCheckoutResult("https://checkout.paymongo.com/cs_123", "cs_123"));

        var resolver = Substitute.For<ITenantPaymentProviderResolver>();
        resolver.ResolveAsync(TenantId, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(mockProvider);

        var signer = Substitute.For<IBookingUrlSigner>();
        signer.GenerateSignedUrl(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns("https://test.com/pay/success?sig=abc");

        var pageOptions = Options.Create(new PaymentPageOptions { BaseUrl = "https://test.com/pay" });

        var handler = new CreatePublicCheckoutHandler(
            bookingRepo, tenantRepo, resolver, signer, pageOptions);

        return (handler, mockProvider, bookingRepo);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsCheckoutUrl()
    {
        var (handler, _, _) = Build();

        var result = await handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        result.CheckoutUrl.Should().Be("https://checkout.paymongo.com/cs_123");
    }

    [Fact]
    public async Task Handle_ValidRequest_StoresCheckoutDetailsOnBooking()
    {
        var (handler, _, bookingRepo) = Build();

        await handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        await bookingRepo.Received(1).UpdateAsync(
            Arg.Is<Booking>(b => b.CheckoutUrl == "https://checkout.paymongo.com/cs_123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BookingNotPendingPayment_Throws()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.Confirmed)
            .Build();
        var (handler, _, _) = Build(booking);

        var act = () => handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task Handle_PassesHmacSignedSuccessUrl()
    {
        var (handler, provider, _) = Build();

        await handler.Handle(new CreatePublicCheckoutCommand
        {
            TenantSlug = "test-tenant",
            BookingId = BookingId,
            ProviderName = "PayMongo"
        }, CancellationToken.None);

        await provider.Received(1).CreateCheckoutSessionAsync(
            Arg.Is<CreateCheckoutRequest>(r =>
                r.SuccessUrl != null && r.SuccessUrl.Contains("sig=")),
            Arg.Any<CancellationToken>());
    }
}
```

### Step 2: Run tests — should fail

### Step 3: Implement command, validator, handler

```csharp
// src/Chronith.Application/Commands/Public/CreatePublicCheckoutCommand.cs
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Options;

namespace Chronith.Application.Commands.Public;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreatePublicCheckoutCommand : IRequest<CreateCheckoutResult>
{
    public required string TenantSlug { get; init; }
    public required Guid BookingId { get; init; }
    public required string ProviderName { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class CreatePublicCheckoutValidator : AbstractValidator<CreatePublicCheckoutCommand>
{
    public CreatePublicCheckoutValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty();
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.ProviderName).NotEmpty().MaximumLength(50);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CreatePublicCheckoutHandler(
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    ITenantPaymentProviderResolver resolver,
    IBookingUrlSigner signer,
    IOptions<PaymentPageOptions> pageOptions)
    : IRequestHandler<CreatePublicCheckoutCommand, CreateCheckoutResult>
{
    public async Task<CreateCheckoutResult> Handle(
        CreatePublicCheckoutCommand cmd, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetBySlugAsync(cmd.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        var booking = await bookingRepo.GetPublicByIdAsync(tenant.Id, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        if (booking.Status != BookingStatus.PendingPayment)
            throw new InvalidStateTransitionException(booking.Status, "create checkout");

        var provider = await resolver.ResolveAsync(tenant.Id, cmd.ProviderName, ct)
            ?? throw new NotFoundException("PaymentProvider", cmd.ProviderName);

        var baseUrl = pageOptions.Value.BaseUrl;
        var successUrl = signer.GenerateSignedUrl($"{baseUrl}/success", cmd.BookingId, cmd.TenantSlug);
        var failureUrl = signer.GenerateSignedUrl($"{baseUrl}/failed", cmd.BookingId, cmd.TenantSlug);

        var checkoutResult = await provider.CreateCheckoutSessionAsync(
            new CreateCheckoutRequest(
                AmountInCentavos: booking.AmountInCentavos,
                Currency: booking.Currency,
                Description: $"Booking {booking.Id}",
                BookingId: booking.Id,
                TenantId: tenant.Id,
                SuccessUrl: successUrl,
                CancelUrl: failureUrl),
            ct);

        booking.SetCheckoutDetails(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId);
        await bookingRepo.UpdateAsync(booking, ct);

        return checkoutResult;
    }
}
```

### Step 4: Run tests

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~CreatePublicCheckoutCommandHandlerTests" -v n`
Expected: All pass.

### Step 5: Commit

```
feat(app): add CreatePublicCheckoutCommand for on-demand checkout session creation
```

---

## Task 7: GetVerifiedBookingQuery + Handler + Unit Tests

**Files:**

- Create: `src/Chronith.Application/Queries/Public/GetVerifiedBookingQuery.cs`
- Create: `tests/Chronith.Tests.Unit/Application/GetVerifiedBookingQueryHandlerTests.cs`

### Step 1: Write failing tests

```csharp
// tests/Chronith.Tests.Unit/Application/GetVerifiedBookingQueryHandlerTests.cs
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetVerifiedBookingQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ValidBooking_ReturnsDto()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.PendingPayment)
            .WithAmount(50_000)
            .Build();

        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var handler = new GetVerifiedBookingQueryHandler(repo);

        var result = await handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        result.Id.Should().Be(BookingId);
        result.Status.Should().Be(BookingStatus.PendingPayment);
        result.AmountInCentavos.Should().Be(50_000);
    }

    [Fact]
    public async Task Handle_BookingNotFound_ThrowsNotFoundException()
    {
        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(default(Booking));

        var handler = new GetVerifiedBookingQueryHandler(repo);

        var act = () => handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PendingPayment_ExposesCheckoutUrl()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.PendingPayment)
            .WithCheckoutUrl("https://checkout.paymongo.com/cs_123")
            .Build();

        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var handler = new GetVerifiedBookingQueryHandler(repo);
        var result = await handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        result.CheckoutUrl.Should().Be("https://checkout.paymongo.com/cs_123");
    }

    [Fact]
    public async Task Handle_Confirmed_HidesCheckoutUrl()
    {
        var booking = new BookingBuilder()
            .WithTenantId(TenantId)
            .WithId(BookingId)
            .InStatus(BookingStatus.Confirmed)
            .WithCheckoutUrl("https://checkout.paymongo.com/cs_123")
            .Build();

        var repo = Substitute.For<IBookingRepository>();
        repo.GetPublicByIdAsync(TenantId, BookingId, Arg.Any<CancellationToken>())
            .Returns(booking);

        var handler = new GetVerifiedBookingQueryHandler(repo);
        var result = await handler.Handle(
            new GetVerifiedBookingQuery(TenantId, BookingId), CancellationToken.None);

        result.CheckoutUrl.Should().BeNull();
    }
}
```

### Step 2: Implement query + handler

```csharp
// src/Chronith.Application/Queries/Public/GetVerifiedBookingQuery.cs
using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Queries.Public;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetVerifiedBookingQuery(Guid TenantId, Guid BookingId)
    : IRequest<PublicBookingStatusDto>, IQuery;

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetVerifiedBookingQueryHandler(IBookingRepository bookingRepository)
    : IRequestHandler<GetVerifiedBookingQuery, PublicBookingStatusDto>
{
    public async Task<PublicBookingStatusDto> Handle(
        GetVerifiedBookingQuery query, CancellationToken ct)
    {
        var booking = await bookingRepository.GetPublicByIdAsync(query.TenantId, query.BookingId, ct)
            ?? throw new NotFoundException("Booking", query.BookingId);

        var checkoutUrl = booking.Status == BookingStatus.PendingPayment
            ? booking.CheckoutUrl
            : null;

        return new PublicBookingStatusDto(
            Id: booking.Id,
            Status: booking.Status,
            Start: booking.Start,
            End: booking.End,
            AmountInCentavos: booking.AmountInCentavos,
            Currency: booking.Currency,
            PaymentReference: booking.PaymentReference,
            CheckoutUrl: checkoutUrl);
    }
}
```

**Note:** This is intentionally similar to `GetPublicBookingStatusQuery`. The difference is that this query is used behind HMAC authentication (the endpoint validates the signature), while the existing one is a fully public endpoint. They could be merged later, but keeping them separate preserves backward compatibility.

### Step 3: Run tests

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~GetVerifiedBookingQueryHandlerTests" -v n`
Expected: All pass.

### Step 4: Commit

```
feat(app): add GetVerifiedBookingQuery for HMAC-authenticated booking fetch
```

---

## Task 8: PublicCreateCheckoutEndpoint + PublicVerifyBookingEndpoint

**Files:**

- Create: `src/Chronith.API/Endpoints/Public/PublicCreateCheckoutEndpoint.cs`
- Create: `src/Chronith.API/Endpoints/Public/PublicVerifyBookingEndpoint.cs`

### Step 1: Implement PublicCreateCheckoutEndpoint

```csharp
// src/Chronith.API/Endpoints/Public/PublicCreateCheckoutEndpoint.cs
using Chronith.Application.Commands.Public;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicCreateCheckoutRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public Guid BookingId { get; set; }
    public long Expires { get; set; }
    public string Sig { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
}

public sealed class PublicCreateCheckoutEndpoint(
    ISender sender,
    IBookingUrlSigner signer)
    : Endpoint<PublicCreateCheckoutRequest, CreateCheckoutResult>
{
    public override void Configure()
    {
        Post("/public/{tenantSlug}/bookings/{bookingId}/checkout");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicCreateCheckoutRequest req, CancellationToken ct)
    {
        if (!signer.Validate(req.BookingId, req.TenantSlug, req.Expires, req.Sig))
            throw new UnauthorizedException("Invalid or expired booking access token.");

        var result = await sender.Send(new CreatePublicCheckoutCommand
        {
            TenantSlug = req.TenantSlug,
            BookingId = req.BookingId,
            ProviderName = req.ProviderName
        }, ct);

        await Send.OkAsync(result, ct);
    }
}
```

### Step 2: Implement PublicVerifyBookingEndpoint

```csharp
// src/Chronith.API/Endpoints/Public/PublicVerifyBookingEndpoint.cs
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Public;
using Chronith.Domain.Exceptions;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.Public;

public sealed class PublicVerifyBookingRequest
{
    public string TenantSlug { get; set; } = string.Empty;
    public Guid BookingId { get; set; }
    public long Expires { get; set; }
    public string Sig { get; set; } = string.Empty;
}

public sealed class PublicVerifyBookingEndpoint(
    ISender sender,
    ITenantRepository tenantRepo,
    IBookingUrlSigner signer)
    : Endpoint<PublicVerifyBookingRequest, PublicBookingStatusDto>
{
    public override void Configure()
    {
        Get("/public/{tenantSlug}/bookings/{bookingId}/verify");
        AllowAnonymous();
        Options(x => x.WithTags("Public").RequireRateLimiting("Public"));
    }

    public override async Task HandleAsync(PublicVerifyBookingRequest req, CancellationToken ct)
    {
        if (!signer.Validate(req.BookingId, req.TenantSlug, req.Expires, req.Sig))
            throw new UnauthorizedException("Invalid or expired booking access token.");

        var tenant = await tenantRepo.GetBySlugAsync(req.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", req.TenantSlug);

        var result = await sender.Send(
            new GetVerifiedBookingQuery(tenant.Id, req.BookingId), ct);

        await Send.OkAsync(result, ct);
    }
}
```

### Step 3: Build

Run: `dotnet build Chronith.slnx`
Expected: Compilation success. FastEndpoints auto-discovers endpoints.

### Step 4: Commit

```
feat(api): add PublicCreateCheckout and PublicVerifyBooking endpoints with HMAC auth
```

---

## Task 9: Functional Tests + Update Existing Tests

**Files:**

- Create: `tests/Chronith.Tests.Functional/Payments/PublicCheckoutEndpointTests.cs`
- Create: `tests/Chronith.Tests.Functional/Payments/PublicVerifyBookingEndpointTests.cs`
- Modify: `tests/Chronith.Tests.Functional/Payments/PaymentFlowTests.cs`
- Modify: `tests/Chronith.Tests.Functional/Public/PublicBookingEndpointsTests.cs`
- Modify: `tests/Chronith.Tests.Functional/Fixtures/FunctionalTestFixture.cs` (add PaymentPage settings)

### Step 1: Update FunctionalTestFixture

Add PaymentPage settings to the WebApplicationFactory configuration:

```csharp
builder.UseSetting("PaymentPage:BaseUrl", "https://test.example.com/pay");
builder.UseSetting("PaymentPage:TokenLifetimeSeconds", "3600");
```

### Step 2: Update PaymentFlowTests

The following tests need updating because Automatic+Stub bookings no longer create checkout sessions at creation:

- `CreateBooking_AutomaticWithStub_ReturnsCheckoutUrl` → Assert `CheckoutUrl` is null, `PaymentUrl` is not null
- `CreateBooking_AutomaticWithStub_HasPaymentReference` → Assert `PaymentReference` is null (set later on checkout)

### Step 3: Update PublicBookingEndpointsTests

The `PublicCreateBooking_Returns201` test may need updating if the booking type is paid+automatic (but the seeded type is Manual+10000 centavos, so it should be fine — no payment URL generated for Manual mode).

### Step 4: Write new functional tests

```csharp
// tests/Chronith.Tests.Functional/Payments/PublicCheckoutEndpointTests.cs
[Collection("Functional")]
public sealed class PublicCheckoutEndpointTests(FunctionalTestFixture fixture)
{
    // Test: POST /public/{tenantSlug}/bookings/{id}/checkout with valid HMAC → 200 + checkout URL
    // Test: POST with invalid/expired HMAC → 401
    // Test: POST for booking not in PendingPayment → 409 (conflict/invalid state)
    // Test: POST with unknown provider → 404
}

// tests/Chronith.Tests.Functional/Payments/PublicVerifyBookingEndpointTests.cs
[Collection("Functional")]
public sealed class PublicVerifyBookingEndpointTests(FunctionalTestFixture fixture)
{
    // Test: GET /public/{tenantSlug}/bookings/{id}/verify with valid HMAC → 200 + booking details
    // Test: GET with invalid/expired HMAC → 401
    // Test: GET for nonexistent booking → 404
}
```

**Important:** Functional tests need to generate HMAC tokens. The test can resolve `IBookingUrlSigner` from the DI container:

```csharp
var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
var url = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, "test-tenant");
// Parse query params from URL to get expires + sig
```

### Step 5: Run all tests

Run: `dotnet test Chronith.slnx -v n`
Expected: All pass.

### Step 6: Commit

```
test: add functional tests for HMAC payment flow and update existing payment tests
```

---

## Task 10: Full Build + Test Pass + Cleanup

### Step 1: Full build

Run: `dotnet build Chronith.slnx`
Expected: 0 errors, 0 warnings (or only pre-existing warnings).

### Step 2: Full test suite

Run: `dotnet test Chronith.slnx -v n`
Expected: All tests pass.

### Step 3: Verify no secrets committed

Verify `appsettings.json` only has placeholder values for `HmacKey` and no real credentials.

### Step 4: Final commit (if needed)

```
chore: clean up any remaining issues from HMAC payment flow implementation
```

---

## Dependency Graph

```
Task 1 (Signer)
  ↓
Task 2 (DTO changes) ───────────┐
  ↓                              │
Task 3 (BookingDto + Mapper)     │
  ↓                              │
Task 4 (CreateBookingHandler)    │
  ↓                              │
Task 5 (PublicCreateBookingHandler)
  ↓                              ↓
Task 6 (CreatePublicCheckoutCommand) ←─┘
  ↓
Task 7 (GetVerifiedBookingQuery)
  ↓
Task 8 (Endpoints)
  ↓
Task 9 (Functional Tests)
  ↓
Task 10 (Full Pass)
```
