using System.Text.RegularExpressions;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Chronith.Application.Interfaces;
using Chronith.Application.Options;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Storage;

public sealed partial class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public AzureBlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var connectionString = options.Value.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException(
                "BlobStorageOptions.ConnectionString must not be empty.",
                nameof(options));

        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<FileUploadResult> UploadAsync(
        string containerName,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        var sanitized = SanitizeFileName(fileName);
        var blobClient = container.GetBlobClient(sanitized);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct);

        return new FileUploadResult(blobClient.Uri.ToString(), sanitized);
    }

    public async Task<Stream?> DownloadAsync(
        string containerName,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = container.GetBlobClient(fileName);

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
            var memoryStream = new MemoryStream();
            await using (response.Value.Content)
            {
                await response.Value.Content.CopyToAsync(memoryStream, ct);
            }
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(
        string containerName,
        string fileName,
        CancellationToken ct = default)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = container.GetBlobClient(fileName);
            await blobClient.DeleteAsync(cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // No-op if blob does not exist
        }
    }

    internal static string SanitizeFileName(string fileName)
    {
        return InvalidBlobCharsRegex().Replace(fileName, "_");
    }

    [GeneratedRegex(@"[^\w\.\-/]")]
    private static partial Regex InvalidBlobCharsRegex();
}
