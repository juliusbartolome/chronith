using Chronith.Infrastructure.Services;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class AuditPiiRedactorTests
{
    private readonly AuditPiiRedactor _sut = new();

    [Fact]
    public void Redact_NullInput_ReturnsNull()
    {
        _sut.Redact(null).Should().BeNull();
    }

    [Fact]
    public void Redact_EmptyString_ReturnsEmptyString()
    {
        _sut.Redact(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void Redact_EmailField_IsRedacted()
    {
        var json = """{"Email":"user@example.com","Name":"Alice"}""";
        var result = _sut.Redact(json)!;
        result.Should().Contain("\"[REDACTED]\"");
        result.Should().NotContain("user@example.com");
        result.Should().Contain("Alice");
    }

    [Fact]
    public void Redact_CustomerEmailField_IsRedacted()
    {
        var json = """{"CustomerEmail":"cust@example.com","Status":"Confirmed"}""";
        var result = _sut.Redact(json)!;
        result.Should().NotContain("cust@example.com");
        result.Should().Contain("Confirmed");
    }

    [Fact]
    public void Redact_PhoneField_IsRedacted()
    {
        var json = """{"Phone":"+63-912-345-6789","Name":"Bob"}""";
        var result = _sut.Redact(json)!;
        result.Should().NotContain("+63-912-345-6789");
        result.Should().Contain("Bob");
    }

    [Fact]
    public void Redact_PasswordHashField_IsRedacted()
    {
        var json = """{"PasswordHash":"$argon2id$v=19$...","Name":"Charlie"}""";
        var result = _sut.Redact(json)!;
        result.Should().NotContain("$argon2id");
        result.Should().Contain("Charlie");
    }

    [Fact]
    public void Redact_NonPiiFields_AreNotAffected()
    {
        var json = """{"Id":"abc-123","Status":"Active","BookingTypeId":"def-456"}""";
        var result = _sut.Redact(json)!;
        result.Should().Contain("abc-123");
        result.Should().Contain("Active");
        result.Should().Contain("def-456");
    }

    [Fact]
    public void Redact_InvalidJson_ReturnsOriginal()
    {
        var notJson = "not json at all";
        _sut.Redact(notJson).Should().Be(notJson);
    }
}
