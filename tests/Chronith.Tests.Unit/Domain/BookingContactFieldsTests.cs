using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class BookingContactFieldsTests
{
    [Fact]
    public void Create_WithContactFields_SetsValues()
    {
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            "cust-1", "test@example.com",
            10000, "PHP",
            firstName: "Julius", lastName: "Bartolome", mobile: "+639171234567");

        booking.FirstName.Should().Be("Julius");
        booking.LastName.Should().Be("Bartolome");
        booking.Mobile.Should().Be("+639171234567");
    }

    [Fact]
    public void Create_WithoutContactFields_DefaultsToEmpty()
    {
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            "cust-1", "test@example.com",
            10000, "PHP");

        booking.FirstName.Should().Be(string.Empty);
        booking.LastName.Should().Be(string.Empty);
        booking.Mobile.Should().BeNull();
    }

    [Fact]
    public void LinkCustomerAccount_SetsCustomerAccountId()
    {
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            "cust-1", "test@example.com",
            10000, "PHP");

        var customerId = Guid.NewGuid();
        booking.LinkCustomerAccount(customerId);

        booking.CustomerAccountId.Should().Be(customerId);
    }
}
