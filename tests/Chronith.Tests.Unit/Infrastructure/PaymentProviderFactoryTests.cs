using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Payments;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class PaymentProviderFactoryTests
{
    [Theory]
    [InlineData("PayMongo")]
    [InlineData("Maya")]
    [InlineData("Stub")]
    public void GetProvider_WithValidName_ReturnsCorrectProvider(string name)
    {
        var factory = BuildFactory();

        var provider = factory.GetProvider(name);

        provider.ProviderName.Should().Be(name);
    }

    [Fact]
    public void GetProvider_WithUnknownName_ThrowsInvalidOperationException()
    {
        var factory = BuildFactory();

        var act = () => factory.GetProvider("Unknown");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown*");
    }

    [Fact]
    public void GetProvider_IsCaseInsensitive()
    {
        var factory = BuildFactory();

        var provider = factory.GetProvider("paymongo");

        provider.ProviderName.Should().Be("PayMongo");
    }

    [Fact]
    public void GetProvider_CaseInsensitive_Maya()
    {
        var factory = BuildFactory();

        var provider = factory.GetProvider("MAYA");

        provider.ProviderName.Should().Be("Maya");
    }

    [Fact]
    public void GetProvider_CaseInsensitive_Stub()
    {
        var factory = BuildFactory();

        var provider = factory.GetProvider("stub");

        provider.ProviderName.Should().Be("Stub");
    }

    private static PaymentProviderFactory BuildFactory()
    {
        var payMongo = CreateMockProvider("PayMongo");
        var maya = CreateMockProvider("Maya");
        var stub = CreateMockProvider("Stub");

        return new PaymentProviderFactory([payMongo, maya, stub]);
    }

    private static IPaymentProvider CreateMockProvider(string name)
    {
        var provider = Substitute.For<IPaymentProvider>();
        provider.ProviderName.Returns(name);
        return provider;
    }
}
