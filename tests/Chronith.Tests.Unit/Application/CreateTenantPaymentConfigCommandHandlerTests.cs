using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CreateTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private static readonly Guid TenantId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    public CreateTenantPaymentConfigCommandHandlerTests() => _tenantContext.TenantId.Returns(TenantId);

    [Fact]
    public async Task Handle_ApiType_CreatesConfigWithIsActiveFalse()
    {
        var handler = new CreateTenantPaymentConfigCommandHandler(_repo, _tenantContext, _unitOfWork);
        var cmd = new CreateTenantPaymentConfigCommand
        {
            ProviderName = "PayMongo",
            Label = "PayMongo Dev",
            Settings = """{"SecretKey":"sk_test"}""",
            PublicNote = null,
            QrCodeUrl = null
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.ProviderName.Should().Be("PayMongo");
        result.Label.Should().Be("PayMongo Dev");
        result.IsActive.Should().BeFalse();
        await _repo.Received(1).AddAsync(Arg.Any<TenantPaymentConfig>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManualType_CreatesConfigWithIsActiveTrue()
    {
        var handler = new CreateTenantPaymentConfigCommandHandler(_repo, _tenantContext, _unitOfWork);
        var cmd = new CreateTenantPaymentConfigCommand
        {
            ProviderName = "Manual",
            Label = "GCash",
            Settings = "{}",
            PublicNote = "Scan to pay",
            QrCodeUrl = "https://qr.example.com"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsActive.Should().BeTrue();
        result.PublicNote.Should().Be("Scan to pay");
        result.QrCodeUrl.Should().Be("https://qr.example.com");
    }
}
