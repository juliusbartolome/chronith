using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class WaitlistEntryTests
{
    private static WaitlistEntry CreateEntry() =>
        WaitlistEntry.Create(
            tenantId: Guid.NewGuid(),
            bookingTypeId: Guid.NewGuid(),
            staffMemberId: null,
            customerId: "cust-1",
            customerEmail: "cust@example.com",
            desiredStart: DateTimeOffset.UtcNow.AddDays(1),
            desiredEnd: DateTimeOffset.UtcNow.AddDays(1).AddHours(1));

    [Fact]
    public void Create_SetsStatusToWaiting()
    {
        var entry = CreateEntry();

        entry.Id.Should().NotBeEmpty();
        entry.Status.Should().Be(WaitlistStatus.Waiting);
        entry.CustomerId.Should().Be("cust-1");
        entry.CustomerEmail.Should().Be("cust@example.com");
        entry.OfferedAt.Should().BeNull();
        entry.ExpiresAt.Should().BeNull();
        entry.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Offer_SetsStatusToOfferedWithExpiry()
    {
        var entry = CreateEntry();
        var now = DateTimeOffset.UtcNow;
        var ttl = TimeSpan.FromHours(24);

        entry.Offer(now, ttl);

        entry.Status.Should().Be(WaitlistStatus.Offered);
        entry.OfferedAt.Should().Be(now);
        entry.ExpiresAt.Should().Be(now.Add(ttl));
    }

    [Fact]
    public void Accept_FromOffered_TransitionsToConverted()
    {
        var entry = CreateEntry();
        entry.Offer(DateTimeOffset.UtcNow, TimeSpan.FromHours(24));

        entry.Accept();

        entry.Status.Should().Be(WaitlistStatus.Converted);
    }

    [Fact]
    public void Expire_FromOffered_TransitionsToExpired()
    {
        var entry = CreateEntry();
        entry.Offer(DateTimeOffset.UtcNow, TimeSpan.FromHours(24));

        entry.Expire();

        entry.Status.Should().Be(WaitlistStatus.Expired);
    }

    [Fact]
    public void Accept_WhenExpired_Throws()
    {
        var entry = CreateEntry();
        entry.Offer(DateTimeOffset.UtcNow, TimeSpan.FromHours(24));
        entry.Expire();

        var act = () => entry.Accept();

        act.Should().Throw<InvalidStateTransitionException>();
    }

    [Fact]
    public void Offer_WhenAlreadyOffered_Throws()
    {
        var entry = CreateEntry();
        entry.Offer(DateTimeOffset.UtcNow, TimeSpan.FromHours(24));

        var act = () => entry.Offer(DateTimeOffset.UtcNow, TimeSpan.FromHours(24));

        act.Should().Throw<InvalidStateTransitionException>();
    }
}
