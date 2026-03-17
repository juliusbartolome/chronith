using Chronith.Application.Interfaces;
using Chronith.Application.Queries.TenantSettings;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetTenantSettingsQueryHandlerTests
{
    private readonly ITenantSettingsRepository _repo = Substitute.For<ITenantSettingsRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    [Fact]
    public async Task Handle_ReturnsSettingsDto()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        var settings = TenantSettings.Create(tenantId);
        _repo.GetOrCreateAsync(tenantId, Arg.Any<CancellationToken>()).Returns(settings);

        var handler = new GetTenantSettingsHandler(_tenantContext, _repo);
        var result = await handler.Handle(new GetTenantSettingsQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
        result.PrimaryColor.Should().Be("#2563EB");
        result.BookingPageEnabled.Should().BeTrue();
    }
}
