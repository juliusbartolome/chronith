using Chronith.Application.Commands.TenantSettings;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class UpdateTenantSettingsCommandHandlerTests
{
    private readonly ITenantSettingsRepository _repo = Substitute.For<ITenantSettingsRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_UpdatesBranding_AndCallsUpdateAndSave()
    {
        var tenantId = Guid.NewGuid();
        _tenantContext.TenantId.Returns(tenantId);
        var settings = TenantSettings.Create(tenantId);
        _repo.GetOrCreateAsync(tenantId, Arg.Any<CancellationToken>()).Returns(settings);

        var handler = new UpdateTenantSettingsCommandHandler(_repo, _tenantContext, _uow);
        var result = await handler.Handle(new UpdateTenantSettingsCommand
        {
            LogoUrl = "https://example.com/logo.png",
            PrimaryColor = "#FF5733",
            AccentColor = "#33FF57",
            WelcomeMessage = "Welcome!"
        }, CancellationToken.None);

        result.LogoUrl.Should().Be("https://example.com/logo.png");
        result.PrimaryColor.Should().Be("#FF5733");
        result.AccentColor.Should().Be("#33FF57");
        result.WelcomeMessage.Should().Be("Welcome!");
        await _repo.Received(1).UpdateAsync(settings, Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
