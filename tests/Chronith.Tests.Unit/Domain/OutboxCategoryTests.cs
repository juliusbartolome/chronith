using Chronith.Domain.Enums;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public class OutboxCategoryTests
{
    [Fact]
    public void OutboxCategory_TenantWebhook_HasValue0()
    {
        ((int)OutboxCategory.TenantWebhook).Should().Be(0);
    }

    [Fact]
    public void OutboxCategory_CustomerCallback_HasValue1()
    {
        ((int)OutboxCategory.CustomerCallback).Should().Be(1);
    }
}
