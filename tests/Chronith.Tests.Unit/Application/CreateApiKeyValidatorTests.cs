using Chronith.Application.Commands.ApiKeys;
using Chronith.Application.Models;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Application;

public sealed class CreateApiKeyValidatorTests
{
    private readonly CreateApiKeyValidator _validator = new();

    [Fact]
    public async Task Validate_ValidScopes_IsValid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "My key",
            Scopes = [ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyScopes_IsInvalid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "My key",
            Scopes = [],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Scopes");
    }

    [Fact]
    public async Task Validate_UnknownScope_IsInvalid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "My key",
            Scopes = [ApiKeyScope.BookingsRead, "totally:invalid"],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("totally:invalid"));
    }

    [Fact]
    public async Task Validate_EmptyDescription_IsInvalid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "",
            Scopes = [ApiKeyScope.BookingsRead],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }
}
