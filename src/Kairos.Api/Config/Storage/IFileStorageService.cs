using Amazon.S3.Model;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Config.Storage;

public interface IFileStorageService
{
    Task<Result<StoredFile>> UploadAsync(UploadFileRequest request, CancellationToken ct = default);
    Task<Result<FileDownload>> DownloadAsync(StorageBucket bucket, string key, CancellationToken ct = default);
    Task<Result<StoredFileInfo>> GetInfoAsync(StorageBucket bucket, string key, CancellationToken ct = default);
    Task<Result<bool>> ExistsAsync(StorageBucket bucket, string key, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(StorageBucket bucket, string key, CancellationToken ct = default);
    Task<Result<int>> DeleteManyAsync(StorageBucket bucket, IEnumerable<string> keys, CancellationToken ct = default);
    Task<Result<bool>> CopyAsync(
        StorageBucket sourceBucket, string sourceKey,
        StorageBucket destinationBucket, string destinationKey,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<StoredFileInfo>>> ListAsync(
        StorageBucket bucket, string prefix, int maxItems = 1000,
        CancellationToken ct = default);

    Result<PresignedUrl> GetDownloadUrl(
        StorageBucket bucket, string key, DownloadUrlOptions? options = null);
}

public sealed class FileDownload(GetObjectResponse response)
    : IDisposable, IAsyncDisposable
{
    public Stream Content => response.ResponseStream;

    public string ContentType =>
        response.Headers.ContentType ?? "application/octet-stream";

    public long ContentLength => response.Headers.ContentLength;

    public string? ETag => response.ETag;

    public DateTimeOffset LastModified => S3Convert.ToUtc(response.LastModified);

    public void Dispose() => response.Dispose();

    public ValueTask DisposeAsync()
    {
        response.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal static class S3Convert
{
    public static long ToLong(long? value) => value ?? 0;

    public static DateTimeOffset ToUtc(DateTime? value) =>
        value is { } v
            ? new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc))
            : DateTimeOffset.UnixEpoch;
}