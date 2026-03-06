using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class BookingTypePricingTests
{
    [Fact]
    public void TimeSlot_Create_SetsPriceInCentavosAndCurrency()
    {
        var bt = TimeSlotBookingType.Create(
            tenantId: Guid.NewGuid(),
            slug: "haircut",
            name: "Haircut",
            capacity: 1,
            paymentMode: PaymentMode.Automatic,
            paymentProvider: "PayMongo",
            durationMinutes: 30,
            bufferBeforeMinutes: 5,
            bufferAfterMinutes: 5,
            availabilityWindows: [],
            priceInCentavos: 50000,
            currency: "PHP");

        bt.PriceInCentavos.Should().Be(50000);
        bt.Currency.Should().Be("PHP");
    }

    [Fact]
    public void Calendar_Create_SetsPriceInCentavosAndCurrency()
    {
        var bt = CalendarBookingType.Create(
            tenantId: Guid.NewGuid(),
            slug: "consultation",
            name: "Consultation",
            capacity: 1,
            paymentMode: PaymentMode.Automatic,
            paymentProvider: "Maya",
            availableDays: [DayOfWeek.Monday],
            priceInCentavos: 100000,
            currency: "PHP");

        bt.PriceInCentavos.Should().Be(100000);
        bt.Currency.Should().Be("PHP");
    }

    [Fact]
    public void Update_ChangesPriceAndCurrency()
    {
        var bt = BookingTypeBuilder.BuildTimeSlot();

        bt.Update(
            name: "Updated",
            capacity: 2,
            paymentMode: PaymentMode.Automatic,
            paymentProvider: "PayMongo",
            durationMinutes: 60,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0,
            availabilityWindows: null,
            availableDays: null,
            priceInCentavos: 75000,
            currency: "PHP");

        bt.PriceInCentavos.Should().Be(75000);
        bt.Currency.Should().Be("PHP");
    }

    [Fact]
    public void Create_WithZeroPrice_Succeeds()
    {
        var bt = TimeSlotBookingType.Create(
            tenantId: Guid.NewGuid(),
            slug: "free-session",
            name: "Free Session",
            capacity: 1,
            paymentMode: PaymentMode.Manual,
            paymentProvider: null,
            durationMinutes: 30,
            bufferBeforeMinutes: 0,
            bufferAfterMinutes: 0,
            availabilityWindows: [],
            priceInCentavos: 0,
            currency: "PHP");

        bt.PriceInCentavos.Should().Be(0);
    }
}
