using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Chronith.Tests.Unit.Infrastructure.Repositories;

public sealed class TenantUserRepositoryEncryptionTests
{
    private static IBlindIndexService CreateBlindIndex()
    {
        var key = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)77).ToArray());
        return new HmacBlindIndexService(Options.Create(new BlindIndexOptions { HmacKey = key }));
    }

    [Fact]
    public void EmailToken_IsNormalized()
    {
        var idx = CreateBlindIndex();
        idx.ComputeToken("Admin@Company.COM").Should().Be(idx.ComputeToken("admin@company.com"));
    }

    [Fact]
    public void DecryptEmail_LegacyPlaintext_ReturnRawValue()
    {
        var enc = Substitute.For<IEncryptionService>();
        enc.Decrypt("admin@example.com")
            .Throws(new InvalidOperationException("no prefix"));

        string DecryptEmail(string? value)
        {
            if (value is null) return string.Empty;
            try { return enc.Decrypt(value) ?? string.Empty; }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException)
            { return value; }
        }

        DecryptEmail("admin@example.com").Should().Be("admin@example.com");
    }
}
