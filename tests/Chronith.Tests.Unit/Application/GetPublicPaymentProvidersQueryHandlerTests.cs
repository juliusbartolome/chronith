using Chronith.Application.Interfaces;
using Chronith.Application.Queries.TenantPaymentConfig;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetPublicPaymentProvidersQueryHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();

    [Fact]
    public async Task Handle_ReturnsOnlyActiveConfigs_AsSummaryDtos()
    {
        var tenantId = Guid.NewGuid();
        var active = TenantPaymentConfig.Create(tenantId, "Manual", "GCash", "{}", "Pay via GCash", "https://qr.example.com");
        _repo.ListActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>()).Returns([active]);

        var handler = new GetPublicPaymentProvidersQueryHandler(_repo);
        var result = await handler.Handle(new GetPublicPaymentProvidersQuery(tenantId), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ProviderName.Should().Be("Manual");
        result[0].Label.Should().Be("GCash");
        result[0].PublicNote.Should().Be("Pay via GCash");
    }

    [Fact]
    public async Task Handle_WhenNoneActive_ReturnsEmpty()
    {
        _repo.ListActiveByTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns([]);

        var handler = new GetPublicPaymentProvidersQueryHandler(_repo);
        var result = await handler.Handle(
            new GetPublicPaymentProvidersQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
