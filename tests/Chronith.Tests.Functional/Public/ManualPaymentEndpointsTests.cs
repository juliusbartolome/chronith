using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Chronith.Tests.Functional.Public;

[Collection("Functional")]
public sealed class ManualPaymentEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "manual-pay-endpoints-type";
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
        await SeedData.SeedTenantPaymentConfigAsync(db,
            providerName: "Manual",
            label: "GCash / Bank Transfer",
            publicNote: "Send via GCash to 0917-xxx-xxxx",
            qrCodeUrl: "https://example.com/qr.png",
            factory: fixture.Factory);
        return (tenantId, bookingTypeId);
    }

    /// <summary>
    /// Creates a booking via the public endpoint and returns its ID + the anonymous client.
    /// Uses a unique day offset to avoid slot conflicts within the same booking type.
    /// </summary>
    private async Task<(Guid BookingId, HttpClient Client)> CreateBookingViaPublicEndpointAsync(int dayOffset)
    {
        var client = fixture.CreateAnonymousClient();
        var payload = new
        {
            StartTime = $"2026-06-{(10 + dayOffset):D2}T10:00:00Z",
            CustomerEmail = $"manual-{Guid.NewGuid():N}@example.com",
            CustomerId = $"manual-cust-{Guid.NewGuid():N}"
        };

        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/booking-types/{BookingTypeSlug}/bookings", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var booking = await response.ReadFromApiJsonAsync<BookingDto>();
        booking.Should().NotBeNull();
        return (booking!.Id, client);
    }

    private (long Expires, string Sig) ExtractHmacParams(string url)
    {
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return (long.Parse(query["expires"]!), query["sig"]!);
    }

    // ── End-to-end flow ──────────────────────────────────────────────────────

    [Fact]
    public async Task FullFlow_CreateBooking_ConfirmPayment_StaffApprove()
    {
        await EnsureSeedAsync();
        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();

        // 1. Create a booking via public endpoint → should be PendingPayment with paymentUrl
        var (bookingId, client) = await CreateBookingViaPublicEndpointAsync(dayOffset: 1);

        // 2. Fetch public booking status → assert Manual payment mode + options populated
        var statusResponse = await client.GetAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var statusDto = await statusResponse.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        statusDto.Should().NotBeNull();
        statusDto!.Status.Should().Be(BookingStatus.PendingPayment);
        statusDto.PaymentMode.Should().Be("Manual");
        statusDto.ManualPaymentOptions.Should().NotBeNull();
        statusDto.ManualPaymentOptions!.QrCodeUrl.Should().Be("https://example.com/qr.png");
        statusDto.ManualPaymentOptions.PublicNote.Should().Be("Send via GCash to 0917-xxx-xxxx");
        statusDto.ManualPaymentOptions.Label.Should().Be("GCash / Bank Transfer");

        // 3. Confirm manual payment with proof file → PendingVerification
        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(customerUrl);

        var confirmContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        confirmContent.Add(fileContent, "ProofFile", "proof.jpg");
        confirmContent.Add(new StringContent("Bank transfer ref ABC-123"), "PaymentNote");

        var confirmResponse = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expires}&sig={sig}",
            confirmContent);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirmDto = await confirmResponse.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        confirmDto.Should().NotBeNull();
        confirmDto!.Status.Should().Be(BookingStatus.PendingVerification);
        confirmDto.ProofOfPaymentUrl.Should().NotBeNullOrEmpty();
        confirmDto.ProofOfPaymentFileName.Should().NotBeNullOrEmpty();
        confirmDto.PaymentNote.Should().Be("Bank transfer ref ABC-123");

        // 4. Staff verify (approve) → Confirmed
        var staffUrl = signer.GenerateStaffVerifyUrl("https://test.example.com/verify", bookingId, TenantSlug);
        var (staffExpires, staffSig) = ExtractHmacParams(staffUrl);

        var verifyResponse = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={staffExpires}&sig={staffSig}",
            new { action = "approve" });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var verifyDto = await verifyResponse.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        verifyDto.Should().NotBeNull();
        verifyDto!.Status.Should().Be(BookingStatus.Confirmed);
    }

    // ── Confirm without proof file ───────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_WithoutProofFile_TransitionsToPendingVerification()
    {
        await EnsureSeedAsync();
        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var (bookingId, client) = await CreateBookingViaPublicEndpointAsync(dayOffset: 2);

        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(customerUrl);

        // Send multipart form without a file — just a note
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("Paid via bank deposit"), "PaymentNote");

        var response = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expires}&sig={sig}",
            content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.PendingVerification);
        dto.ProofOfPaymentUrl.Should().BeNull();
        dto.PaymentNote.Should().Be("Paid via bank deposit");
    }

    // ── Staff reject ─────────────────────────────────────────────────────────

    [Fact]
    public async Task StaffVerify_Reject_TransitionsToCancelled()
    {
        await EnsureSeedAsync();
        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var (bookingId, client) = await CreateBookingViaPublicEndpointAsync(dayOffset: 3);

        // Customer confirms payment first (without proof) to reach PendingVerification
        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(customerUrl);

        var confirmContent = new MultipartFormDataContent();
        confirmContent.Add(new StringContent(""), "PaymentNote");
        var confirmResponse = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expires}&sig={sig}",
            confirmContent);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Staff rejects
        var staffUrl = signer.GenerateStaffVerifyUrl("https://test.example.com/verify", bookingId, TenantSlug);
        var (staffExpires, staffSig) = ExtractHmacParams(staffUrl);

        var rejectResponse = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={staffExpires}&sig={staffSig}",
            new { action = "reject", note = "Proof of payment is unclear" });
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await rejectResponse.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.Cancelled);
    }

    // ── Expired HMAC ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_ExpiredHmac_Returns401()
    {
        await EnsureSeedAsync();
        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var (bookingId, client) = await CreateBookingViaPublicEndpointAsync(dayOffset: 4);

        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (_, sig) = ExtractHmacParams(customerUrl);

        // Use an already-expired timestamp (1 second in the past)
        var expiredTimestamp = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(""), "PaymentNote");
        var response = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expiredTimestamp}&sig={sig}",
            content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StaffVerify_ExpiredHmac_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        // Seed a booking directly in PendingVerification so we can test staff-verify
        var start = DateTimeOffset.UtcNow.AddDays(25);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var staffUrl = signer.GenerateStaffVerifyUrl("https://test.example.com/verify", bookingId, TenantSlug);
        var (_, staffSig) = ExtractHmacParams(staffUrl);

        var expiredTimestamp = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();

        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={expiredTimestamp}&sig={staffSig}",
            new { action = "approve" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Invalid HMAC ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_InvalidHmac_Returns401()
    {
        await EnsureSeedAsync();
        var (bookingId, client) = await CreateBookingViaPublicEndpointAsync(dayOffset: 5);

        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var invalidSig = "this-is-not-a-valid-signature";

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(""), "PaymentNote");
        var response = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expires}&sig={invalidSig}",
            content);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StaffVerify_InvalidHmac_Returns401()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(26);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 10_000);

        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var invalidSig = "this-is-not-a-valid-signature";

        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={expires}&sig={invalidSig}",
            new { action = "approve" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Wrong booking status ─────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmPayment_WhenAlreadyPendingVerification_Returns422()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(27);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var customerUrl = signer.GenerateSignedUrl("https://test.example.com/pay", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(customerUrl);

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(""), "PaymentNote");
        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/confirm-payment?expires={expires}&sig={sig}",
            content);
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task StaffVerify_WhenStillPendingPayment_Returns422()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(28);
        var bookingId = await SeedData.SeedBookingAsync(db,
            bookingTypeId, start, start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 10_000);

        var signer = fixture.Factory.Services.GetRequiredService<IBookingUrlSigner>();
        var staffUrl = signer.GenerateStaffVerifyUrl("https://test.example.com/verify", bookingId, TenantSlug);
        var (expires, sig) = ExtractHmacParams(staffUrl);

        var client = fixture.CreateAnonymousClient();
        var response = await client.PostAsJsonAsync(
            $"/v1/public/{TenantSlug}/bookings/{bookingId}/staff-verify?expires={expires}&sig={sig}",
            new { action = "approve" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
