using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class CustomFieldValidationTests
{
    [Fact]
    public void Validate_ValidData_DoesNotThrow()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("name", "text", Required: true),
            new("age", "number", Required: false)
        };

        var json = """{"name":"Alice","age":30}""";

        var act = () => CustomFieldValidator.Validate(json, schema);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MissingRequiredField_Throws()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("name", "text", Required: true)
        };

        var json = """{}""";

        var act = () => CustomFieldValidator.Validate(json, schema);

        act.Should().Throw<CustomFieldValidationException>()
            .WithMessage("*Required custom field missing*name*");
    }

    [Fact]
    public void Validate_WrongType_Throws()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("age", "number", Required: true)
        };

        var json = """{"age":"not-a-number"}""";

        var act = () => CustomFieldValidator.Validate(json, schema);

        act.Should().Throw<CustomFieldValidationException>()
            .WithMessage("*must be of type*number*");
    }

    [Fact]
    public void Validate_UnknownField_Throws()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("name", "text", Required: false)
        };

        var json = """{"name":"Alice","extra":"data"}""";

        var act = () => CustomFieldValidator.Validate(json, schema);

        act.Should().Throw<CustomFieldValidationException>()
            .WithMessage("*Unknown custom field*extra*");
    }

    [Fact]
    public void Validate_EmptySchema_DoesNotThrow()
    {
        var schema = new List<CustomFieldDefinition>();

        var act = () => CustomFieldValidator.Validate("""{"anything":"here"}""", schema);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_NullJsonWithNoRequired_DoesNotThrow()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("name", "text", Required: false)
        };

        var act = () => CustomFieldValidator.Validate(null, schema);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_NullJsonWithRequired_Throws()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("name", "text", Required: true)
        };

        var act = () => CustomFieldValidator.Validate(null, schema);

        act.Should().Throw<CustomFieldValidationException>()
            .WithMessage("*Missing required custom fields*");
    }

    [Fact]
    public void Validate_BooleanField_AcceptsTrueAndFalse()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("active", "boolean", Required: true)
        };

        var act = () => CustomFieldValidator.Validate("""{"active":true}""", schema);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DateField_AcceptsValidDate()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("dob", "date", Required: true)
        };

        var act = () => CustomFieldValidator.Validate("""{"dob":"2024-01-15T00:00:00Z"}""", schema);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_DateField_RejectsInvalidDate()
    {
        var schema = new List<CustomFieldDefinition>
        {
            new("dob", "date", Required: true)
        };

        var act = () => CustomFieldValidator.Validate("""{"dob":"not-a-date"}""", schema);

        act.Should().Throw<CustomFieldValidationException>()
            .WithMessage("*must be of type*date*");
    }
}
