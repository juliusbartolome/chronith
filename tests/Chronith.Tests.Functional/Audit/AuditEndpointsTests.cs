using System.Net;
using System.Net.Http.Json;
using Chronith.Application.DTOs;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Tests.Functional.Fixtures;
using Chronith.Tests.Functional.Helpers;

namespace Chronith.Tests.Functional.Audit;

[Collection("Functional")]
public sealed class AuditEndpointsTests(FunctionalTestFixture fixture)
{
    private async Task<(Guid EntryId1, Guid EntryId2)> EnsureSeedAsync()
    {
        await using var db = SeedData.CreateDbContext(fixture.Factory);
        await SeedData.SeedTenantAsync(db);

        var entry1 = new AuditEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = TestConstants.TenantId,
            UserId = TestConstants.AdminUserId,
            UserRole = "TenantAdmin",
            EntityType = "Booking",
            EntityId = Guid.NewGuid(),
            Action = "Create",
            OldValues = null,
            NewValues = """{"id":"some-id"}""",
            Metadata = null,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var entry2 = new AuditEntryEntity
        {
            Id = Guid.NewGuid(),
            TenantId = TestConstants.TenantId,
            UserId = TestConstants.AdminUserId,
            UserRole = "TenantAdmin",
            EntityType = "StaffMember",
            EntityId = Guid.NewGuid(),
            Action = "Update",
            OldValues = """{"name":"old"}""",
            NewValues = """{"name":"new"}""",
            Metadata = null,
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-2)
        };

        db.AuditEntries.Add(entry1);
        db.AuditEntries.Add(entry2);
        await db.SaveChangesAsync();

        return (entry1.Id, entry2.Id);
    }

    [Fact]
    public async Task GetAuditEntries_AsAdmin_ReturnsPagedResult()
    {
        var (entryId1, _) = await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadFromApiJsonAsync<PagedResultDto<AuditEntryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetAuditEntries_FilterByEntityType_ReturnsFilteredResult()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/audit?entityType=Booking");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadFromApiJsonAsync<PagedResultDto<AuditEntryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().AllSatisfy(e => e.EntityType.Should().Be("Booking"));
    }

    [Fact]
    public async Task GetAuditEntries_FilterByAction_ReturnsFilteredResult()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/audit?action=Update");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadFromApiJsonAsync<PagedResultDto<AuditEntryDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().AllSatisfy(e => e.Action.Should().Be("Update"));
    }

    [Fact]
    public async Task GetAuditEntries_Pagination_RespectsPageSize()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync("/v1/audit?page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.ReadFromApiJsonAsync<PagedResultDto<AuditEntryDto>>();
        result.Should().NotBeNull();
        result!.Items.Count.Should().Be(1);
        result.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task GetAuditEntryById_AsAdmin_ReturnsEntry()
    {
        var (entryId1, _) = await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync($"/v1/audit/{entryId1}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = await response.ReadFromApiJsonAsync<AuditEntryDto>();
        entry.Should().NotBeNull();
        entry!.Id.Should().Be(entryId1);
        entry.EntityType.Should().Be("Booking");
        entry.Action.Should().Be("Create");
    }

    [Fact]
    public async Task GetAuditEntryById_NonExistent_Returns404()
    {
        await EnsureSeedAsync();
        var client = fixture.CreateClient("TenantAdmin");

        var response = await client.GetAsync($"/v1/audit/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
