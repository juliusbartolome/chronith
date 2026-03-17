using FluentAssertions;
using Xunit;

namespace Chronith.Client.Tests;

public class ChronithClientOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new ChronithClientOptions { BaseUrl = "http://localhost" };

        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.MaxRetries.Should().Be(3);
        options.ApiKey.Should().BeNull();
        options.JwtToken.Should().BeNull();
    }
}
