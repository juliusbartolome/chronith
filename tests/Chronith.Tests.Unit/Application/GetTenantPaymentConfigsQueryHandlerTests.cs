using Chronith.Application.Interfaces;
using Chronith.Application.Queries.TenantPaymentConfig;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetTenantPaymentConfigsQueryHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private static readonly Guid TenantId = Guid.Parse("22222222-0000-0000-0000-000000000001");

    public GetTenantPaymentConfigsQueryHandlerTests() => _tenantContext.TenantId.Returns(TenantId);

    [Fact]
    public async Task Handle_ReturnsAllNonDeletedConfigsForTenant()
    {
        var configs = new List<TenantPaymentConfig>
        {
            TenantPaymentConfig.Create(TenantId, "PayMongo", "Dev", """{"SecretKey":"sk_1"}""", null, null),
            TenantPaymentConfig.Create(TenantId, "Manual", "GCash", "{}", "Pay via GCash", null)
        };
        _repo.ListByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(configs);

        var handler = new GetTenantPaymentConfigsQueryHandler(_repo, _tenantContext);
        var result = await handler.Handle(new GetTenantPaymentConfigsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.ProviderName).Should().Contain(["PayMongo", "Manual"]);
    }

    [Fact]
    public async Task Handle_WhenNoConfigs_ReturnsEmptyList()
    {
        _repo.ListByTenantAsync(TenantId, Arg.Any<CancellationToken>())
             .Returns(new List<TenantPaymentConfig>());

        var handler = new GetTenantPaymentConfigsQueryHandler(_repo, _tenantContext);
        var result = await handler.Handle(new GetTenantPaymentConfigsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
