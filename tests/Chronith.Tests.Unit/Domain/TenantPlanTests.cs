using Chronith.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Chronith.Tests.Unit.Domain;

public class TenantPlanTests
{
    [Fact]
    public void Create_WithValidData_ReturnsTenantPlan()
    {
        var plan = TenantPlan.Create(
            name: "Pro",
            maxBookingTypes: 25,
            maxStaffMembers: 15,
            maxBookingsPerMonth: 5000,
            maxCustomers: 5000,
            notificationsEnabled: true,
            analyticsEnabled: true,
            customBrandingEnabled: true,
            apiAccessEnabled: true,
            auditLogEnabled: true,
            priceCentavos: 490000,
            sortOrder: 2);

        plan.Id.Should().NotBeEmpty();
        plan.Name.Should().Be("Pro");
        plan.MaxBookingTypes.Should().Be(25);
        plan.MaxStaffMembers.Should().Be(15);
        plan.MaxBookingsPerMonth.Should().Be(5000);
        plan.MaxCustomers.Should().Be(5000);
        plan.NotificationsEnabled.Should().BeTrue();
        plan.AnalyticsEnabled.Should().BeTrue();
        plan.CustomBrandingEnabled.Should().BeTrue();
        plan.ApiAccessEnabled.Should().BeTrue();
        plan.AuditLogEnabled.Should().BeTrue();
        plan.PriceCentavos.Should().Be(490000);
        plan.SortOrder.Should().Be(2);
        plan.IsActive.Should().BeTrue();
        plan.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var plan = TenantPlan.Create(
            name: "Free", maxBookingTypes: 1, maxStaffMembers: 0,
            maxBookingsPerMonth: 50, maxCustomers: 50,
            notificationsEnabled: false, analyticsEnabled: false,
            customBrandingEnabled: false, apiAccessEnabled: false,
            auditLogEnabled: false, priceCentavos: 0, sortOrder: 0);

        plan.Deactivate();

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var plan = TenantPlan.Create(
            name: "Free", maxBookingTypes: 1, maxStaffMembers: 0,
            maxBookingsPerMonth: 50, maxCustomers: 50,
            notificationsEnabled: false, analyticsEnabled: false,
            customBrandingEnabled: false, apiAccessEnabled: false,
            auditLogEnabled: false, priceCentavos: 0, sortOrder: 0);
        plan.Deactivate();

        plan.Activate();

        plan.IsActive.Should().BeTrue();
    }
}
