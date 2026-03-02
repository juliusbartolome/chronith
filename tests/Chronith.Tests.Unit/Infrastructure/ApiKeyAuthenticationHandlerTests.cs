using System.Security.Claims;
using System.Text.Encodings.Web;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public sealed class ApiKeyAuthenticationHandlerTests
{
    // Helper to build an initialized handler
    private static async Task<ApiKeyAuthenticationHandler> BuildHandlerAsync(
        IApiKeyRepository apiKeyRepo,
        string? headerValue = null)
    {
        var options = new ApiKeyAuthenticationOptions();
        var optionsMonitor = Substitute.For<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        optionsMonitor.Get(Arg.Any<string>()).Returns(options);
        optionsMonitor.CurrentValue.Returns(options);

        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var encoder = UrlEncoder.Default;

        var handler = new ApiKeyAuthenticationHandler(optionsMonitor, loggerFactory, encoder, apiKeyRepo);

        var context = new DefaultHttpContext();
        if (headerValue is not null)
            context.Request.Headers["X-Api-Key"] = headerValue;

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationOptions.SchemeLabel,
            null,
            typeof(ApiKeyAuthenticationHandler));

        await ((IAuthenticationHandler)handler).InitializeAsync(scheme, context);
        return handler;
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NoHeader_ReturnsNoResult()
    {
        // Arrange
        var repo = Substitute.For<IApiKeyRepository>();
        var handler = await BuildHandlerAsync(repo, headerValue: null);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.None.Should().BeTrue("no X-Api-Key header should yield NoResult");
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidKey_ReturnsSuccess_WithClaims()
    {
        // Arrange
        var (rawKey, hash) = TenantApiKey.GenerateKey();
        var tenantId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var apiKey = new TenantApiKey
        {
            Id = keyId,
            TenantId = tenantId,
            KeyHash = hash,
            Description = "test",
            Role = "Admin",
        };

        var repo = Substitute.For<IApiKeyRepository>();
        repo.GetByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(apiKey);

        var handler = await BuildHandlerAsync(repo, headerValue: rawKey);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();

        var claims = result.Principal!.Claims.ToList();
        claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == keyId.ToString());
        claims.Should().Contain(c => c.Type == "sub" && c.Value == keyId.ToString());
    }

    [Fact]
    public async Task HandleAuthenticateAsync_RevokedOrUnknownKey_ReturnsFail()
    {
        // Arrange
        var (rawKey, _) = TenantApiKey.GenerateKey();

        var repo = Substitute.For<IApiKeyRepository>();
        // GetByHashAsync returns null for revoked/unknown keys
        repo.GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((TenantApiKey?)null);

        var handler = await BuildHandlerAsync(repo, headerValue: rawKey);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeFalse("a known-bad key should Fail, not NoResult");
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Contain("Invalid or revoked API key");
    }
}
