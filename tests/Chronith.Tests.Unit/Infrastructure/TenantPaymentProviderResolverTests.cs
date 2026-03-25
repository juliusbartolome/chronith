using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class TenantPaymentProviderResolverTests
{
    private readonly ITenantPaymentConfigRepository _repo =
        Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IHttpClientFactory _httpClientFactory =
        Substitute.For<IHttpClientFactory>();

    private TenantPaymentProviderResolver CreateSut() =>
        new(_repo, _httpClientFactory, NullLoggerFactory.Instance);

    [Fact]
    public async Task ResolveAsync_Stub_AlwaysReturnsProvider_WithoutDbLookup()
    {
        var sut = CreateSut();

        var result = await sut.ResolveAsync(Guid.NewGuid(), "Stub");

        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("Stub");
        await _repo.DidNotReceive().GetActiveByProviderNameAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_Manual_AlwaysReturnsNull_WithoutDbLookup()
    {
        var sut = CreateSut();

        var result = await sut.ResolveAsync(Guid.NewGuid(), "Manual");

        result.Should().BeNull();
        await _repo.DidNotReceive().GetActiveByProviderNameAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_PayMongo_WhenNoActiveConfig_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        _repo.GetActiveByProviderNameAsync(tenantId, "PayMongo", Arg.Any<CancellationToken>())
             .Returns(default(TenantPaymentConfig));

        var sut = CreateSut();
        var result = await sut.ResolveAsync(tenantId, "PayMongo");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_PayMongo_WhenActiveConfigExists_ReturnsProvider()
    {
        var tenantId = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "Dev",
            """{"SecretKey":"sk","PublicKey":"pk","WebhookSecret":"ws","SuccessUrl":"https://ok","FailureUrl":"https://fail"}""",
            null, null);
        _repo.GetActiveByProviderNameAsync(tenantId, "PayMongo", Arg.Any<CancellationToken>())
             .Returns(config);

        var sut = CreateSut();
        var result = await sut.ResolveAsync(tenantId, "PayMongo");

        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("PayMongo");
    }

    [Fact]
    public async Task ResolveAsync_Maya_WhenNoActiveConfig_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        _repo.GetActiveByProviderNameAsync(tenantId, "Maya", Arg.Any<CancellationToken>())
             .Returns(default(TenantPaymentConfig));

        var sut = CreateSut();
        var result = await sut.ResolveAsync(tenantId, "Maya");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_UnknownProvider_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        _repo.GetActiveByProviderNameAsync(tenantId, "Stripe", Arg.Any<CancellationToken>())
             .Returns(default(TenantPaymentConfig));

        var sut = CreateSut();
        var result = await sut.ResolveAsync(tenantId, "Stripe");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_IsCaseInsensitive_Stub()
    {
        var sut = CreateSut();

        var result = await sut.ResolveAsync(Guid.NewGuid(), "stub");

        result.Should().NotBeNull();
        result!.ProviderName.Should().Be("Stub");
    }
}
