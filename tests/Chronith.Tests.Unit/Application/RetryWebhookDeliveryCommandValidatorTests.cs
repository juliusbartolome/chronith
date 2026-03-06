using Chronith.Application.Commands.Webhooks.RetryWebhookDelivery;
using FluentAssertions;

namespace Chronith.Tests.Unit.Application;

public class RetryWebhookDeliveryCommandValidatorTests
{
    private readonly RetryWebhookDeliveryCommandValidator _sut = new();

    [Fact]
    public void Validate_WithEmptyWebhookId_ReturnsError()
    {
        var cmd = new RetryWebhookDeliveryCommand(Guid.Empty, Guid.NewGuid());
        var result = _sut.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RetryWebhookDeliveryCommand.WebhookId));
    }

    [Fact]
    public void Validate_WithEmptyDeliveryId_ReturnsError()
    {
        var cmd = new RetryWebhookDeliveryCommand(Guid.NewGuid(), Guid.Empty);
        var result = _sut.Validate(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RetryWebhookDeliveryCommand.DeliveryId));
    }

    [Fact]
    public void Validate_WithValidIds_ReturnsSuccess()
    {
        var cmd = new RetryWebhookDeliveryCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = _sut.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }
}
