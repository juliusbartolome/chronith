namespace Chronith.Application.Interfaces;

public interface IFileStorageService
{
    Task<FileUploadResult> UploadAsync(
        string containerName,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    Task<Stream?> DownloadAsync(
        string containerName,
        string fileName,
        CancellationToken ct = default);

    Task DeleteAsync(
        string containerName,
        string fileName,
        CancellationToken ct = default);
}

public sealed record FileUploadResult(string Url, string FileName);
