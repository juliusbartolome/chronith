using Chronith.Application.Behaviors;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class AuditBehaviorTests
{
    private readonly IAuditEntryRepository _auditRepo = Substitute.For<IAuditEntryRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public AuditBehaviorTests()
    {
        _tenantContext.TenantId.Returns(Guid.NewGuid());
        _tenantContext.UserId.Returns("user-123");
        _tenantContext.Role.Returns("Admin");
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    private sealed record AuditableRequest : IRequest<string>, IAuditable
    {
        public Guid EntityId { get; init; }
        public string EntityType { get; init; } = "Booking";
        public string Action { get; init; } = "Created";
    }

    private sealed record NonAuditableRequest : IRequest<string>;

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenRequestIsAuditable_CapturesSnapshotsAndPersistsAuditEntry()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var request = new AuditableRequest { EntityId = entityId };

        var resolver = Substitute.For<IAuditSnapshotResolver>();
        resolver.EntityType.Returns("Booking");
        resolver.ResolveSnapshotAsync(entityId, Arg.Any<CancellationToken>())
            .Returns("""{"status":"PendingPayment"}""", """{"status":"Confirmed"}""");

        var behavior = new AuditBehavior<AuditableRequest, string>(
            [resolver], _auditRepo, _tenantContext, _unitOfWork);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");

        await resolver.Received(2).ResolveSnapshotAsync(entityId, Arg.Any<CancellationToken>());

        await _auditRepo.Received(1).AddAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityType == "Booking" &&
                e.EntityId == entityId &&
                e.Action == "Created" &&
                e.OldValues == """{"status":"PendingPayment"}""" &&
                e.NewValues == """{"status":"Confirmed"}""" &&
                e.UserId == "user-123" &&
                e.UserRole == "Admin"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRequestIsNotAuditable_CallsNextWithoutAuditing()
    {
        // Arrange
        var resolver = Substitute.For<IAuditSnapshotResolver>();
        var behavior = new AuditBehavior<NonAuditableRequest, string>(
            [resolver], _auditRepo, _tenantContext, _unitOfWork);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("ok");

        // Act
        var result = await behavior.Handle(new NonAuditableRequest(), next, CancellationToken.None);

        // Assert
        result.Should().Be("ok");
        await _auditRepo.DidNotReceive().AddAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoResolverFoundForEntityType_AuditsWithNullSnapshots()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var request = new AuditableRequest
        {
            EntityId = entityId,
            EntityType = "UnknownType",
            Action = "Deleted"
        };

        // No resolvers matching "UnknownType"
        var behavior = new AuditBehavior<AuditableRequest, string>(
            [], _auditRepo, _tenantContext, _unitOfWork);

        RequestHandlerDelegate<string> next = _ => Task.FromResult("deleted");

        // Act
        var result = await behavior.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().Be("deleted");

        await _auditRepo.Received(1).AddAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityType == "UnknownType" &&
                e.EntityId == entityId &&
                e.Action == "Deleted" &&
                e.OldValues == null &&
                e.NewValues == null),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
