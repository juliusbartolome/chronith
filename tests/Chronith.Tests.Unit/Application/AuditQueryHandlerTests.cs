using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Application.Queries.Audit;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class AuditQueryHandlerTests
{
    private readonly IAuditEntryRepository _auditRepo = Substitute.For<IAuditEntryRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly Guid _tenantId = Guid.NewGuid();

    public AuditQueryHandlerTests()
    {
        _tenantContext.TenantId.Returns(_tenantId);
    }

    // ── GetAuditEntryByIdQuery ────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditEntryById_WhenFound_ReturnsDto()
    {
        var entry = AuditEntry.Create(
            _tenantId, "user-1", "Admin", "Booking",
            Guid.NewGuid(), "Created", null, """{"status":"ok"}""", null);

        _auditRepo.GetByIdAsync(_tenantId, entry.Id, Arg.Any<CancellationToken>())
            .Returns(entry);

        var handler = new GetAuditEntryByIdHandler(_tenantContext, _auditRepo);
        var result = await handler.Handle(new GetAuditEntryByIdQuery(entry.Id), CancellationToken.None);

        result.Id.Should().Be(entry.Id);
        result.UserId.Should().Be("user-1");
        result.UserRole.Should().Be("Admin");
        result.EntityType.Should().Be("Booking");
        result.Action.Should().Be("Created");
        result.NewValues.Should().Be("""{"status":"ok"}""");
    }

    [Fact]
    public async Task GetAuditEntryById_WhenNotFound_ThrowsNotFoundException()
    {
        var id = Guid.NewGuid();
        _auditRepo.GetByIdAsync(_tenantId, id, Arg.Any<CancellationToken>())
            .Returns(default(AuditEntry));

        var handler = new GetAuditEntryByIdHandler(_tenantContext, _auditRepo);

        var act = async () => await handler.Handle(new GetAuditEntryByIdQuery(id), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ── GetAuditEntriesQuery ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditEntries_ReturnsPagedResult()
    {
        var entry = AuditEntry.Create(
            _tenantId, "user-1", "Admin", "Booking",
            Guid.NewGuid(), "Created", null, null, null);

        _auditRepo.QueryAsync(
                _tenantId, "Booking", null, null, null, null, null, 1, 20,
                Arg.Any<CancellationToken>())
            .Returns(([entry], 1));

        var handler = new GetAuditEntriesHandler(_tenantContext, _auditRepo);
        var query = new GetAuditEntriesQuery(
            EntityType: "Booking",
            EntityId: null,
            UserId: null,
            Action: null,
            From: null,
            To: null,
            Page: 1,
            PageSize: 20);

        var result = await handler.Handle(query, CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items[0].EntityType.Should().Be("Booking");
    }

    // ── AuditEntryMapper ──────────────────────────────────────────────────────

    [Fact]
    public void ToDto_MapsAllProperties()
    {
        var entityId = Guid.NewGuid();
        var entry = AuditEntry.Create(
            Guid.NewGuid(), "user-1", "Staff", "Booking",
            entityId, "Updated",
            """{"old":true}""", """{"new":true}""", """{"ip":"1.2.3.4"}""");

        var dto = entry.ToDto();

        dto.Id.Should().Be(entry.Id);
        dto.UserId.Should().Be("user-1");
        dto.UserRole.Should().Be("Staff");
        dto.EntityType.Should().Be("Booking");
        dto.EntityId.Should().Be(entityId);
        dto.Action.Should().Be("Updated");
        dto.OldValues.Should().Be("""{"old":true}""");
        dto.NewValues.Should().Be("""{"new":true}""");
        dto.Metadata.Should().Be("""{"ip":"1.2.3.4"}""");
        dto.Timestamp.Should().Be(entry.Timestamp);
    }
}
