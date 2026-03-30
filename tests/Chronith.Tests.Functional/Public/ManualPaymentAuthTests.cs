using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.Public;

[Collection("Functional")]
public sealed class ManualPaymentAuthTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "manual-pay-auth-type";
    private const string TenantSlug = "test-tenant";

    private async Task<(Guid TenantId, Guid BookingTypeId)> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var tenantId = await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db,
            slug: BookingTypeSlug,
            capacity: 10,
            priceInCentavos: 10_000,
            paymentMode: PaymentMode.Manual);
        await SeedData.SeedTenantPaymentConfigAsync(db, factory: fixture.Factory);
        return (tenantId, bookingTypeId);
    }

    private (long Expires, string Sig) ExtractHmacParams(string url)
    {
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return (long.Parse(query["expires"]!), query["sig"]!);
    }

    // ── Valid HMAC — endpoints work for anonymous callers ─────────────────────

    [Fact]
    public async Task ConfirmPayment_ValidHmac_Anonymous_Returns200()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(40);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(customerUrl);

        var client = fixture.CreateAnonymousClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(""), "PaymentNote");
        var response = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expires}&sig={sig}",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.PendingVerification);
    }

    [Fact]
    public async Task StaffVerify_ValidHmac_Anonymous_Returns200()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(41);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var staffUrl = signer.GenerateStaffVerifyUrl("https://test.example.com/verify", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(staffUrl);

        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={expires}&sig={sig}",
            new { action = "approve" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.Confirmed);
    }

    // ── Expired signatures ───────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_ExpiredSignature_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(42);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (_, sig) = ExtractHmacParams(customerUrl);

        // Use an already-expired timestamp
        var expiredTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds();

        var client = fixture.CreateAnonymousClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(""), "PaymentNote");
        var response = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expiredTimestamp}&sig={sig}",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StaffVerify_ExpiredSignature_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(43);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var staffUrl = signer.GenerateStaffVerifyUrl("https://test.example.com/verify", bookingId, TenantSlug);
        var (_, sig) = ExtractHmacParams(staffUrl);

        var expiredTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds();

        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={expiredTimestamp}&sig={sig}",
            new { action = "approve" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Domain separation: customer sig → staff endpoint is rejected ─────────

    [Fact]
    public async Task StaffVerify_WithCustomerDomainSignature_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(44);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();

        // Generate a customer-domain (booking-access) signature
        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(customerUrl);

        // Send the customer-domain sig to the staff-verify endpoint
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={expires}&sig={sig}",
            new { action = "approve" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "customer booking-access signatures must not be accepted by the staff-verify endpoint");
    }

    // ── Domain separation: staff sig → customer endpoint is rejected ─────────

    [Fact]
    public async Task ConfirmPayment_WithStaffDomainSignature_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(45);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();

        // Generate a staff-verify domain signature
        var staffUrl = signer.GenerateStaffVerifyUrl("https://test.example.com/verify", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(staffUrl);

        // Send the staff-domain sig to the confirm-payment endpoint
        var client = fixture.CreateAnonymousClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(""), "PaymentNote");
        var response = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expires}&sig={sig}",
            content);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "staff-verify signatures must not be accepted by the confirm-payment endpoint");
    }
}
