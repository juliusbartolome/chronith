using Chronith.Application.Options;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure;

public class RedisOptionsTests
{
    [Fact]
    public void RedisOptions_DefaultEnabled_IsFalse()
    {
        var opts = new RedisOptions();
        opts.Enabled.Should().BeFalse();
    }

    [Fact]
    public void RedisOptions_SectionName_IsRedis()
    {
        RedisOptions.SectionName.Should().Be("Redis");
    }
}
