using Chronith.Application.Options;
using Chronith.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.Infrastructure.Storage;

public class AzureBlobStorageServiceTests
{
    // ── constructor validation ────────────────────────────────────────────────

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenConnectionStringIsEmpty()
    {
        var options = Options.Create(new BlobStorageOptions
        {
            ConnectionString = string.Empty
        });

        var act = () => new AzureBlobStorageService(options);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ConnectionString*");
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenConnectionStringIsNull()
    {
        var options = Options.Create(new BlobStorageOptions
        {
            ConnectionString = null!
        });

        var act = () => new AzureBlobStorageService(options);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ConnectionString*");
    }

    [Fact]
    public void Constructor_Succeeds_WithAzuriteConnectionString()
    {
        var options = Options.Create(new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true"
        });

        var act = () => new AzureBlobStorageService(options);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_Succeeds_WithFullAzuriteConnectionString()
    {
        var options = Options.Create(new BlobStorageOptions
        {
            ConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1"
        });

        var act = () => new AzureBlobStorageService(options);

        act.Should().NotThrow();
    }
}
