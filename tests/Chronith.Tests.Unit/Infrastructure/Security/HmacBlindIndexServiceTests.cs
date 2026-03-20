using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public sealed class HmacBlindIndexServiceTests
{
    private static IBlindIndexService CreateService()
    {
        // 32 bytes of 0x01 — valid HMAC key
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)1).ToArray());
        var options = Options.Create(new BlindIndexOptions { HmacKey = key });
        return new HmacBlindIndexService(options);
    }

    [Fact]
    public void ComputeToken_SameInput_ReturnsSameToken()
    {
        var svc = CreateService();
        var t1 = svc.ComputeToken("user@example.com");
        var t2 = svc.ComputeToken("user@example.com");
        t1.Should().Be(t2);
    }

    [Fact]
    public void ComputeToken_DifferentInputs_ReturnsDifferentTokens()
    {
        var svc = CreateService();
        svc.ComputeToken("a@b.com").Should().NotBe(svc.ComputeToken("c@d.com"));
    }

    [Fact]
    public void ComputeToken_NormalizesToLowercase()
    {
        var svc = CreateService();
        svc.ComputeToken("User@Example.COM").Should().Be(svc.ComputeToken("user@example.com"));
    }

    [Fact]
    public void ComputeToken_Returns64CharHexString()
    {
        var svc = CreateService();
        var token = svc.ComputeToken("test@test.com");
        token.Should().HaveLength(64);
        token.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Constructor_InvalidKey_Throws()
    {
        var options = Options.Create(new BlindIndexOptions { HmacKey = "not-valid-base64!!!" });
        var act = () => new HmacBlindIndexService(options);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HmacKey*");
    }
}
