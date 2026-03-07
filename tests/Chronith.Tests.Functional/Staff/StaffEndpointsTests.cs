using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Domain.Enums;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Tests.Functional.Staff;

[Collection("Functional")]
public sealed class StaffEndpointsTests(FunctionalTestFixture fixture)
{
    private const string BookingTypeSlug = "staff-endpoints-type";

    private async Task EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);
        await SeedData.SeedBookingTypeAsync(db, BookingTypeSlug);
    }

    [Fact]
    public async Task CreateStaff_AsAdmin_Returns201WithId()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.PostAsJsonAsync("/staff", new
        {
            name = "Alice Test",
            email = $"alice-{Guid.NewGuid():N}@example.com",
            availabilityWindows = new[]
            {
                new { dayOfWeek = 1, startTime = "09:00", endTime = "17:00" }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var staff = await response.Content.ReadFromJsonAsync<StaffMemberDto>();
        staff.Should().NotBeNull();
        staff!.Id.Should().NotBeEmpty();
        staff.Name.Should().Be("Alice Test");
        staff.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ListStaff_AsAdmin_ReturnsStaffMembers()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a staff member first
        var uniqueEmail = $"list-staff-{Guid.NewGuid():N}@example.com";
        var createResp = await client.PostAsJsonAsync("/staff", new
        {
            name = "List Test Staff",
            email = uniqueEmail,
            availabilityWindows = Array.Empty<object>()
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<StaffMemberDto>();

        // List and verify
        var listResp = await client.GetAsync("/staff");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var staffList = await listResp.Content.ReadFromJsonAsync<List<StaffMemberDto>>();
        staffList.Should().NotBeNull();
        staffList!.Should().Contain(s => s.Id == created!.Id);
    }

    [Fact]
    public async Task GetStaff_AsStaff_Returns200()
    {
        await EnsureSeedAsync();
        var adminClient = fixture.CreateClient("TenantAdmin");

        // Create via admin
        var createResp = await adminClient.PostAsJsonAsync("/staff", new
        {
            name = "Get Test Staff",
            email = $"get-staff-{Guid.NewGuid():N}@example.com",
            availabilityWindows = Array.Empty<object>()
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<StaffMemberDto>();

        // Get as TenantStaff role
        var staffClient = fixture.CreateClient("TenantStaff");
        var getResp = await staffClient.GetAsync($"/staff/{created!.Id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var staff = await getResp.Content.ReadFromJsonAsync<StaffMemberDto>();
        staff.Should().NotBeNull();
        staff!.Id.Should().Be(created.Id);
        staff.Name.Should().Be("Get Test Staff");
    }

    [Fact]
    public async Task UpdateStaff_AsAdmin_Returns200WithUpdatedData()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create
        var createResp = await client.PostAsJsonAsync("/staff", new
        {
            name = "Original Name",
            email = $"update-staff-{Guid.NewGuid():N}@example.com",
            availabilityWindows = Array.Empty<object>()
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<StaffMemberDto>();

        // Update
        var updateResp = await client.PutAsJsonAsync($"/staff/{created!.Id}", new
        {
            name = "Updated Name",
            email = created.Email,
            availabilityWindows = new[]
            {
                new { dayOfWeek = 0, startTime = "10:00", endTime = "14:00" }
            }
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<StaffMemberDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
        updated.AvailabilityWindows.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteStaff_AsAdmin_Returns204()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create
        var createResp = await client.PostAsJsonAsync("/staff", new
        {
            name = "Delete Test Staff",
            email = $"delete-staff-{Guid.NewGuid():N}@example.com",
            availabilityWindows = Array.Empty<object>()
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<StaffMemberDto>();

        // Delete
        var deleteResp = await client.DeleteAsync($"/staff/{created!.Id}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone from list
        var listResp = await client.GetAsync("/staff");
        var staffList = await listResp.Content.ReadFromJsonAsync<List<StaffMemberDto>>();
        staffList!.Should().NotContain(s => s.Id == created.Id);
    }

    [Fact]
    public async Task AssignStaff_ToBooking_Returns200()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        // Create a staff member
        var staffResp = await client.PostAsJsonAsync("/staff", new
        {
            name = "Assign Test Staff",
            email = $"assign-staff-{Guid.NewGuid():N}@example.com",
            availabilityWindows = Array.Empty<object>()
        });
        staffResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var staff = await staffResp.Content.ReadFromJsonAsync<StaffMemberDto>();

        // Seed a booking
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        var btId = (await db.BookingTypes.FirstAsync(bt => bt.Slug == BookingTypeSlug)).Id;
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var bookingId = await SeedData.SeedBookingAsync(db, btId, start, start.AddHours(1),
            BookingStatus.Confirmed);

        // Assign staff
        var assignResp = await client.PostAsJsonAsync($"/bookings/{bookingId}/assign-staff", new
        {
            staffMemberId = staff!.Id
        });

        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
