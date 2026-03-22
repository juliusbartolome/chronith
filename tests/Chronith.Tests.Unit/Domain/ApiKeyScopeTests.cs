// tests/Chronith.Tests.Unit/Domain/ApiKeyScopeTests.cs
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class ApiKeyScopeTests
{
    [Fact]
    public void All_ContainsExactlyTwentyScopes()
    {
        ApiKeyScope.All.Should().HaveCount(20);
    }

    [Fact]
    public void All_ContainsNoNullOrEmpty()
    {
        ApiKeyScope.All.Should().AllSatisfy(s => s.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void ConstantValues_AreAllUnique()
    {
        var constantValues = typeof(ApiKeyScope)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        constantValues.Should().OnlyHaveUniqueItems(
            "no two ApiKeyScope constants should have the same string value");
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
    [InlineData(ApiKeyScope.WebhooksRead)]
    [InlineData(ApiKeyScope.WebhooksWrite)]
    [InlineData(ApiKeyScope.TenantRead)]
    [InlineData(ApiKeyScope.TenantWrite)]
    [InlineData(ApiKeyScope.AuditRead)]
    [InlineData(ApiKeyScope.NotificationsWrite)]
    [InlineData(ApiKeyScope.NotificationTemplatesWrite)]
    [InlineData(ApiKeyScope.TimeBlocksWrite)]
    public void EachConstant_IsInAll(string scope)
    {
        ApiKeyScope.All.Should().Contain(scope);
    }
}
