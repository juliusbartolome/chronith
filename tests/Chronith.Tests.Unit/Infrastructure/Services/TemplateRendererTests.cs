using Chronith.Infrastructure.Services.Notifications;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class TemplateRendererTests
{
    private readonly TemplateRenderer _sut = new();

    [Fact]
    public void Render_ReplacesAllPlaceholders_WithContextValues()
    {
        var template = "Hello {{customer_name}}, your booking for {{service_name}} is confirmed.";
        var context = new Dictionary<string, string>
        {
            ["customer_name"] = "Alice",
            ["service_name"] = "Massage Therapy"
        };

        var result = _sut.Render(template, context);

        result.Should().Be("Hello Alice, your booking for Massage Therapy is confirmed.");
    }

    [Fact]
    public void Render_LeavesUnknownVariables_AsIs()
    {
        var template = "Hello {{customer_name}}, your booking {{booking_id}} is confirmed.";
        var context = new Dictionary<string, string>
        {
            ["customer_name"] = "Bob"
        };

        var result = _sut.Render(template, context);

        result.Should().Be("Hello Bob, your booking {{booking_id}} is confirmed.");
    }

    [Fact]
    public void Render_NullTemplate_ReturnsNull()
    {
        var result = _sut.Render(null!, new Dictionary<string, string>());

        result.Should().BeNull();
    }

    [Fact]
    public void Render_EmptyTemplate_ReturnsEmpty()
    {
        var result = _sut.Render(string.Empty, new Dictionary<string, string>());

        result.Should().Be(string.Empty);
    }

    [Fact]
    public void Render_TemplateWithNoVariables_ReturnsAsIs()
    {
        var template = "Your booking has been confirmed. Thank you for choosing us!";
        var context = new Dictionary<string, string>
        {
            ["customer_name"] = "Charlie"
        };

        var result = _sut.Render(template, context);

        result.Should().Be("Your booking has been confirmed. Thank you for choosing us!");
    }

    [Fact]
    public void Render_NullValueInContext_ReplacesWithEmptyString()
    {
        var template = "Hello {{customer_name}}, your status is {{status}}.";
        var context = new Dictionary<string, string>
        {
            ["customer_name"] = "Diana",
            ["status"] = null!
        };

        var result = _sut.Render(template, context);

        result.Should().Be("Hello Diana, your status is .");
    }

    [Fact]
    public void Render_MultipleOccurrencesOfSameVariable_ReplacesAll()
    {
        var template = "{{name}}, we confirm your booking. Thanks, {{name}}!";
        var context = new Dictionary<string, string>
        {
            ["name"] = "Eve"
        };

        var result = _sut.Render(template, context);

        result.Should().Be("Eve, we confirm your booking. Thanks, Eve!");
    }
}
