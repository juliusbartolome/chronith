// tests/Chronith.Tests.Unit/Domain/ApiKeyScopeTests.cs
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class ApiKeyScopeTests
{
    [Fact]
    public void All_ContainsExactlyFifteenScopes()
    {
        ApiKeyScope.All.Should().HaveCount(15);
    }

    [Fact]
    public void All_ContainsNoNullOrEmpty()
    {
        ApiKeyScope.All.Should().AllSatisfy(s => s.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void All_ContainsNoDuplicates()
    {
        var distinct = ApiKeyScope.All.Distinct().ToList();
        distinct.Should().HaveCount(ApiKeyScope.All.Count);
    }

    [Theory]
    [InlineData(ApiKeyScope.BookingsRead)]
    [InlineData(ApiKeyScope.BookingsWrite)]
    [InlineData(ApiKeyScope.BookingsDelete)]
    [InlineData(ApiKeyScope.BookingsConfirm)]
    [InlineData(ApiKeyScope.BookingsCancel)]
    [InlineData(ApiKeyScope.BookingsPay)]
    [InlineData(ApiKeyScope.AvailabilityRead)]
    [InlineData(ApiKeyScope.StaffRead)]
    [InlineData(ApiKeyScope.StaffWrite)]
    [InlineData(ApiKeyScope.BookingTypesRead)]
    [InlineData(ApiKeyScope.BookingTypesWrite)]
    [InlineData(ApiKeyScope.AnalyticsRead)]
    [InlineData(ApiKeyScope.WebhooksWrite)]
    [InlineData(ApiKeyScope.TenantRead)]
    [InlineData(ApiKeyScope.TenantWrite)]
    public void EachConstant_IsInAll(string scope)
    {
        ApiKeyScope.All.Should().Contain(scope);
    }
}
