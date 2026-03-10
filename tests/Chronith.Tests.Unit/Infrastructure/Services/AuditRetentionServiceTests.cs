using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class AuditRetentionServiceTests
{
    private static (
        AuditRetentionService Service,
        IAuditEntryRepository AuditRepo)
        BuildSut(
            IEnumerable<Guid>? tenantIds = null,
            int retentionDays = 90,
            int cleanupIntervalHours = 24)
    {
        var auditRepo = Substitute.For<IAuditEntryRepository>();

        auditRepo
            .GetDistinctTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns((tenantIds ?? []).ToList().AsReadOnly() as IReadOnlyList<Guid>
                     ?? new List<Guid>().AsReadOnly());

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAuditEntryRepository)).Returns(auditRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var options = Options.Create(new AuditRetentionOptions
        {
            RetentionDays = retentionDays,
            IntervalHours = cleanupIntervalHours
        });

        var logger = Substitute.For<ILogger<AuditRetentionService>>();
        var healthTracker = Substitute.For<IBackgroundServiceHealthTracker>();

        var sut = new AuditRetentionService(scopeFactory, options, healthTracker, logger);

        return (sut, auditRepo);
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_WithNoTenants_DoesNotCallDeleteExpired()
    {
        var (sut, auditRepo) = BuildSut(tenantIds: []);

        await sut.PurgeExpiredEntriesAsync(CancellationToken.None);

        await auditRepo.DidNotReceive().DeleteExpiredAsync(
            Arg.Any<Guid>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_WithTenants_CallsDeleteExpiredForEachTenant()
    {
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var (sut, auditRepo) = BuildSut(tenantIds: [tenant1, tenant2]);

        await sut.PurgeExpiredEntriesAsync(CancellationToken.None);

        await auditRepo.Received(1).DeleteExpiredAsync(
            tenant1,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());

        await auditRepo.Received(1).DeleteExpiredAsync(
            tenant2,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_UsesCorrectRetentionCutoff()
    {
        var tenantId = Guid.NewGuid();
        const int retentionDays = 30;

        var (sut, auditRepo) = BuildSut(tenantIds: [tenantId], retentionDays: retentionDays);

        var before = DateTimeOffset.UtcNow;
        await sut.PurgeExpiredEntriesAsync(CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        await auditRepo.Received(1).DeleteExpiredAsync(
            tenantId,
            Arg.Is<DateTimeOffset>(d =>
                d >= before.AddDays(-retentionDays).AddSeconds(-1) &&
                d <= after.AddDays(-retentionDays).AddSeconds(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_WhenDeleteThrows_DoesNotPropagateException()
    {
        var tenantId = Guid.NewGuid();

        var (sut, auditRepo) = BuildSut(tenantIds: [tenantId]);
        auditRepo
            .DeleteExpiredAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new InvalidOperationException("db error")));

        var act = async () => await sut.PurgeExpiredEntriesAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
