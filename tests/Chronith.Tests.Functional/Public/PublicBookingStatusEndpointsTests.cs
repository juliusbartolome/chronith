using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Public;

[Collection("Functional")]
public sealed class PublicBookingStatusEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "status-endpoints-type";
    private const string TenantSlug = "test-tenant";

    private async Task<(Guid TenantId, Guid BookingTypeId)> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var tenantId = await SeedData.SeedTenantAsync(db);
        var bookingTypeId = await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
        return (tenantId, bookingTypeId);
    }

    [Fact]
    public async Task GetPublicBookingStatus_WhenPendingPayment_Returns200WithCheckoutUrl()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(2);
        var bookingId = await SeedData.SeedBookingAsync(
            db,
            bookingTypeId,
            start,
            start.AddHours(1),
            status: BookingStatus.PendingPayment,
            amountInCentavos: 50_000L,
            paymentReference: "cs_test_abc123",
            checkoutUrl: "https://checkout.paymongo.com/cs_test_abc123");

        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/bookings/{bookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.PendingPayment);
        dto.CheckoutUrl.Should().Be("https://checkout.paymongo.com/cs_test_abc123");
        dto.PaymentReference.Should().Be("cs_test_abc123");
        dto.AmountInCentavos.Should().Be(50_000L);
        dto.Currency.Should().Be("PHP");
    }

    [Fact]
    public async Task GetPublicBookingStatus_WhenPendingVerification_Returns200WithNullCheckoutUrl()
    {
        var (_, bookingTypeId) = await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var start = DateTimeOffset.UtcNow.AddDays(3);
        var bookingId = await SeedData.SeedBookingAsync(
            db,
            bookingTypeId,
            start,
            start.AddHours(1),
            status: BookingStatus.PendingVerification,
            amountInCentavos: 0L,
            checkoutUrl: "https://checkout.paymongo.com/should-not-show");

        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync($"/v1/public/{TenantSlug}/bookings/{bookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.ReadFromApiJsonAsync<PublicBookingStatusDto>();
        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.PendingVerification);
        dto.CheckoutUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetPublicBookingStatus_UnknownBookingId_Returns404()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateAnonymousClient();

        var response = await client.GetAsync(
            $"/v1/public/{TenantSlug}/bookings/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPublicBookingStatus_CrossTenant_Returns404()
    {
        // Seed a booking under a different tenant — should not be visible via test-tenant slug
        await EnsureSeedAsync();
        await using var db = SeedData.CreateDbContext(fixture.Factory);

        var otherTenantId = Guid.NewGuid();
        await SeedData.SeedTenantAsync(db, otherTenantId, "other-tenant");

        // We need a booking type under the other tenant
        var otherBookingTypeId = Guid.NewGuid();
        db.BookingTypes.Add(new Chronith.Infrastructure.Persistence.Entities.BookingTypeEntity
        {
            Id = otherBookingTypeId,
            TenantId = otherTenantId,
            Slug = "other-type",
            Name = "Other Type",
            Kind = Chronith.Domain.Enums.BookingKind.TimeSlot,
            Capacity = 5,
            DurationMinutes = 60,
            BufferBeforeMinutes = 0,
            BufferAfterMinutes = 0,
            PriceInCentavos = 0,
            Currency = "PHP",
            IsDeleted = false
        });
        await db.SaveChangesAsync();

        var start = DateTimeOffset.UtcNow.AddDays(4);
        var otherBookingId = await SeedData.SeedBookingAsync(
            db,
            otherBookingTypeId,
            start,
            start.AddHours(1),
            tenantId: otherTenantId);

        // Request the other tenant's booking via the test-tenant slug — should be 404
        var client = fixture.CreateAnonymousClient();
        var response = await client.GetAsync(
            $"/v1/public/{TenantSlug}/bookings/{otherBookingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
