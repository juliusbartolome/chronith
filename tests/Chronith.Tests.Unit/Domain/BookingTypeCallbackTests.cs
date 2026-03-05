using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public class BookingTypeCallbackTests
{
    private static BookingType CreateTestBookingType()
    {
        // Use the internal constructor (EF hydration path) on the concrete TimeSlotBookingType
        return (BookingType)typeof(TimeSlotBookingType)
            .GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null, [], null)!
            .Invoke([]);
    }

    [Fact]
    public void SetCustomerCallback_WithUrl_SetsUrlAndGeneratesSecret()
    {
        var bt = CreateTestBookingType();
        const string url = "https://example.com/callback";

        bt.SetCustomerCallback(url);

        bt.CustomerCallbackUrl.Should().Be(url);
        bt.CustomerCallbackSecret.Should().NotBeNullOrEmpty();
        bt.CustomerCallbackSecret!.Length.Should().Be(64); // 32 bytes as hex
    }

    [Fact]
    public void SetCustomerCallback_CalledTwice_GeneratesNewSecret()
    {
        var bt = CreateTestBookingType();
        bt.SetCustomerCallback("https://example.com/1");
        var first = bt.CustomerCallbackSecret;

        bt.SetCustomerCallback("https://example.com/2");

        bt.CustomerCallbackSecret.Should().NotBe(first);
    }

    [Fact]
    public void SetCustomerCallback_WithNull_ClearsBothFields()
    {
        var bt = CreateTestBookingType();
        bt.SetCustomerCallback("https://example.com/callback");
        bt.CustomerCallbackUrl.Should().NotBeNull();

        bt.SetCustomerCallback(null);

        bt.CustomerCallbackUrl.Should().BeNull();
        bt.CustomerCallbackSecret.Should().BeNull();
    }

    [Fact]
    public void CustomerCallbackUrl_DefaultsToNull()
    {
        var bt = CreateTestBookingType();
        bt.CustomerCallbackUrl.Should().BeNull();
    }
}
