using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Storage.Contracts;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.Storage;

public sealed class S3FileStorageService : IFileStorageService, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly IAmazonS3 _presigner;
    private readonly bool _ownsPresigner;
    private readonly Protocol _presignProtocol;
    private readonly StorageOptions _options;
    private readonly ILogger<S3FileStorageService> _logger;

    public S3FileStorageService(
        IAmazonS3 s3,
        IOptions<StorageOptions> options,
        ILogger<S3FileStorageService> logger)
    {
        _s3 = s3;
        _options = options.Value;
        _logger = logger;

        string publicUrl = string.IsNullOrWhiteSpace(_options.PublicUrl)
            ? _options.ServiceUrl
            : _options.PublicUrl;

        // O GetPreSignedURL usa HTTPS por padrão: acompanha o esquema do endereço público
        _presignProtocol = new Uri(publicUrl).Scheme == Uri.UriSchemeHttp
            ? Protocol.HTTP
            : Protocol.HTTPS;

        // Assinar não faz chamada de rede, então um cliente dedicado ao endereço público é barato
        if (string.Equals(publicUrl, _options.ServiceUrl, StringComparison.OrdinalIgnoreCase))
        {
            _presigner = s3;
        }
        else
        {
            _presigner = S3ClientFactory.Create(_options, publicUrl);
            _ownsPresigner = true;
        }
    }

    // ----------------------------------------------------------
    // Upload
    // ----------------------------------------------------------

    public async Task<Result<StoredFile>> UploadAsync(
        UploadFileRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!StorageKeys.IsValidPrefix(request.KeyPrefix))
        {
            return Result<StoredFile>.BadRequest("Invalid storage key prefix.");
        }

        UploadRule rule = GetRule(request.Kind);
        MemoryStream? buffered = null;

        try
        {
            // 1) Precisa ser seekable (para medir, detectar tipo, calcular hash e enviar)
            Stream content = request.Content;

            if (!content.CanSeek)
            {
                Result<MemoryStream> copy = await BufferAsync(content, rule.MaxBytes, ct);

                if (copy.IsFailure)
                {
                    return Result<StoredFile>.Failure(copy.Errors, copy.StatusCode);
                }

                buffered = copy.Value!;
                content = buffered;
            }

            content.Position = 0;
            long length = content.Length;

            if (length == 0)
            {
                return Result<StoredFile>.BadRequest("The file is empty.");
            }

            if (length > rule.MaxBytes)
            {
                return Result<StoredFile>.Failure(
                    $"The file exceeds the maximum size of {rule.MaxBytes / 1024 / 1024} MB.",
                    StatusCodes.Status413PayloadTooLarge);
            }

            // 2) Tipo REAL pelo conteúdo
            string? detected = await FileContentTypes.DetectAsync(content, ct);

            if (detected is null || !rule.AllowedContentTypes.Contains(detected))
            {
                return Result<StoredFile>.Failure(
                    "Unsupported file type.",
                    StatusCodes.Status415UnsupportedMediaType);
            }

            // 3) Hash (integridade e detecção de duplicados)
            content.Position = 0;
            string sha256 = Convert
                .ToHexString(await SHA256.HashDataAsync(content, ct))
                .ToLowerInvariant();

            // 4) Chave gerada por nós: nunca usa o nome enviado pelo usuário
            string key = $"{request.KeyPrefix.TrimEnd('/')}/{Guid.NewGuid():N}" +
                         FileContentTypes.ExtensionFor(detected);

            // 5) Envio
            content.Position = 0;

            PutObjectResponse response = await _s3.PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = BucketName(rule.Bucket),
                    Key = key,
                    InputStream = content,
                    ContentType = detected,
                    AutoCloseStream = false
                },
                ct);

            return Result<StoredFile>.Created(new StoredFile(
                rule.Bucket, key, detected, length, sha256, response.ETag?.Trim('"')));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<StoredFile>(ex, "upload", request.KeyPrefix);
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    // ----------------------------------------------------------
    // Leitura
    // ----------------------------------------------------------

    public async Task<Result<FileDownload>> DownloadAsync(
        StorageBucket bucket, string key, CancellationToken ct = default)
    {
        if (!StorageKeys.IsValidKey(key))
            return Result<FileDownload>.BadRequest("Invalid storage key.");

        try
        {
            GetObjectResponse response = await _s3.GetObjectAsync(
                new GetObjectRequest { BucketName = BucketName(bucket), Key = key }, ct);

            return Result<FileDownload>.Success(new FileDownload(response));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<FileDownload>(ex, "download", key);
        }
    }

    public async Task<Result<StoredFileInfo>> GetInfoAsync(
        StorageBucket bucket, string key, CancellationToken ct = default)
    {
        if (!StorageKeys.IsValidKey(key))
        {
            return Result<StoredFileInfo>.BadRequest("Invalid storage key.");
        }

        try
        {
            GetObjectMetadataResponse head =
                await _s3.GetObjectMetadataAsync(BucketName(bucket), key, ct);

            return Result<StoredFileInfo>.Success(new StoredFileInfo(
                key,
                head.Headers.ContentType,
                head.Headers.ContentLength,
                head.ETag?.Trim('"'),
                S3Convert.ToUtc(head.LastModified)));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<StoredFileInfo>(ex, "head", key);
        }
    }

    public async Task<Result<bool>> ExistsAsync(
        StorageBucket bucket, string key, CancellationToken ct = default)
    {
        Result<StoredFileInfo> info = await GetInfoAsync(bucket, key, ct);

        if (info.IsSuccess)
        {
            return Result<bool>.Success(true);
        }

        return info.StatusCode == StatusCodes.Status404NotFound
            ? Result<bool>.Success(false)
            : Result<bool>.Failure(info.Errors, info.StatusCode);
    }

    public async Task<Result<IReadOnlyList<StoredFileInfo>>> ListAsync(
        StorageBucket bucket, string prefix, int maxItems = 1000, CancellationToken ct = default)
    {
        if (!StorageKeys.IsValidPrefix(prefix))
        {
            return Result<IReadOnlyList<StoredFileInfo>>.BadRequest("Invalid storage key prefix.");
        }

        maxItems = Math.Clamp(maxItems, 1, 10_000);

        try
        {
            var items = new List<StoredFileInfo>();
            string? token = null;

            do
            {
                ListObjectsV2Response response = await _s3.ListObjectsV2Async(
                    new ListObjectsV2Request
                    {
                        BucketName = BucketName(bucket),
                        Prefix = prefix,
                        MaxKeys = Math.Min(1000, maxItems - items.Count),
                        ContinuationToken = token
                    },
                    ct);

                foreach (S3Object item in response.S3Objects ?? [])
                {
                    items.Add(new StoredFileInfo(
                        item.Key,
                        null,
                        S3Convert.ToLong(item.Size),
                        item.ETag?.Trim('"'),
                        S3Convert.ToUtc(item.LastModified)));
                }

                token = response.IsTruncated == true ? response.NextContinuationToken : null;
            }
            while (token is not null && items.Count < maxItems);

            return Result<IReadOnlyList<StoredFileInfo>>.Success(items);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<IReadOnlyList<StoredFileInfo>>(ex, "list", prefix);
        }
    }

    // ----------------------------------------------------------
    // Escrita / remoção
    // ----------------------------------------------------------

    public async Task<Result<bool>> DeleteAsync(
        StorageBucket bucket, string key, CancellationToken ct = default)
    {
        if (!StorageKeys.IsValidKey(key))
        {
            return Result<bool>.BadRequest("Invalid storage key.");
        }

        try
        {
            await _s3.DeleteObjectAsync(BucketName(bucket), key, ct);

            return Result<bool>.Success(true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<bool>(ex, "delete", key);
        }
    }

    public async Task<Result<int>> DeleteManyAsync(
        StorageBucket bucket, IEnumerable<string> keys, CancellationToken ct = default)
    {
        string[] distinct = keys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToArray();

        if (distinct.Any(key => !StorageKeys.IsValidKey(key)))
        {
            return Result<int>.BadRequest("Invalid storage key.");
        }

        int deleted = 0;
        int failed = 0;

        try
        {
            foreach (string[] chunk in distinct.Chunk(1000))
            {
                DeleteObjectsResponse response = await _s3.DeleteObjectsAsync(
                    new DeleteObjectsRequest
                    {
                        BucketName = BucketName(bucket),
                        Objects = chunk.Select(key => new KeyVersion { Key = key }).ToList(),
                        Quiet = true
                    },
                    ct);

                int chunkFailures = response.DeleteErrors?.Count ?? 0;

                failed += chunkFailures;
                deleted += chunk.Length - chunkFailures;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<int>(ex, "delete-many", bucket.ToString());
        }

        if (failed > 0)
        {
            _logger.LogError(
                "Storage delete-many: {Failed} of {Total} objects could not be deleted.",
                failed,
                distinct.Length);

            return Result<int>.Failure(
                $"{failed} file(s) could not be deleted.",
                StatusCodes.Status502BadGateway);
        }

        return Result<int>.Success(deleted);
    }

    public async Task<Result<bool>> CopyAsync(
        StorageBucket sourceBucket, string sourceKey,
        StorageBucket destinationBucket, string destinationKey,
        CancellationToken ct = default)
    {
        if (!StorageKeys.IsValidKey(sourceKey) || !StorageKeys.IsValidKey(destinationKey))
        {
            return Result<bool>.BadRequest("Invalid storage key.");
        }

        try
        {
            await _s3.CopyObjectAsync(
                new CopyObjectRequest
                {
                    SourceBucket = BucketName(sourceBucket),
                    SourceKey = sourceKey,
                    DestinationBucket = BucketName(destinationBucket),
                    DestinationKey = destinationKey
                },
                ct);

            return Result<bool>.Success(true);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<bool>(ex, "copy", sourceKey);
        }
    }

    // ----------------------------------------------------------
    // URL pré-assinada (leitura)
    // ----------------------------------------------------------

    public Result<PresignedUrl> GetDownloadUrl(
        StorageBucket bucket, string key, DownloadUrlOptions? options = null)
    {
        if (!StorageKeys.IsValidKey(key))
        {
            return Result<PresignedUrl>.BadRequest("Invalid storage key.");
        }

        TimeSpan ttl = ResolveTtl(options?.ExpiresIn);

        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = BucketName(bucket),
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.Add(ttl),
                Protocol = _presignProtocol
            };

            if (!string.IsNullOrWhiteSpace(options?.FileName))
            {
                request.ResponseHeaderOverrides.ContentDisposition =
                    BuildContentDisposition(options.FileName, options.Inline);
            }

            string url = _presigner.GetPreSignedURL(request);

            return Result<PresignedUrl>.Success(
                new PresignedUrl(url, DateTimeOffset.UtcNow.Add(ttl)));
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            return Fail<PresignedUrl>(ex, "presign", key);
        }
    }

    // ----------------------------------------------------------
    // Internos
    // ----------------------------------------------------------

    private sealed record UploadRule(
        StorageBucket Bucket,
        IReadOnlySet<string> AllowedContentTypes,
        long MaxBytes);

    private UploadRule GetRule(UploadKind kind) => kind switch
    {
        UploadKind.RoomPhoto => new(
            StorageBucket.RoomMedia, FileContentTypes.Images, _options.Limits.MaxPhotoBytes),

        UploadKind.RoomFloorPlan => new(
            StorageBucket.RoomMedia, FileContentTypes.ImagesAndPdf, _options.Limits.MaxFloorPlanBytes),

        UploadKind.SupportMaterial => new(
            StorageBucket.SupportMaterials, FileContentTypes.SupportDocuments,
            _options.Limits.MaxSupportMaterialBytes),

        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private string BucketName(StorageBucket bucket) => bucket switch
    {
        StorageBucket.RoomMedia => _options.Buckets.RoomMedia,
        StorageBucket.SupportMaterials => _options.Buckets.SupportMaterials,
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, null)
    };

    private TimeSpan ResolveTtl(TimeSpan? requested)
    {
        TimeSpan ttl = requested ?? TimeSpan.FromMinutes(_options.PresignedUrlExpirationMinutes);

        // SigV4 aceita no máximo 7 dias
        return ttl < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1)
             : ttl > TimeSpan.FromDays(7) ? TimeSpan.FromDays(7)
             : ttl;
    }

    private static string BuildContentDisposition(string fileName, bool inline)
    {
        // Remove caminho e caracteres de controle/aspas (evita injeção de header)
        string safe = new string(Path.GetFileName(fileName)
            .Where(c => !char.IsControl(c) && c != '"').ToArray());

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "file";
        }

        // FileNameStar gera filename*=UTF-8''... (RFC 5987), aceita acentos
        return new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
        {
            FileNameStar = safe
        }.ToString();
    }

    private static async Task<Result<MemoryStream>> BufferAsync(
        Stream source, long maxBytes, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        byte[] chunk = ArrayPool<byte>.Shared.Rent(81920);

        try
        {
            int read;

            while ((read = await source.ReadAsync(chunk.AsMemory(), ct)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    buffer.Dispose();

                    return Result<MemoryStream>.Failure(
                        $"The file exceeds the maximum size of {maxBytes / 1024 / 1024} MB.",
                        StatusCodes.Status413PayloadTooLarge);
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }

            buffer.Position = 0;

            return Result<MemoryStream>.Success(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }

    private static bool IsStorageFailure(Exception ex)
        => ex is AmazonClientException or HttpRequestException or IOException or OperationCanceledException;

    private Result<T> Fail<T>(Exception ex, string operation, string? target)
    {
        if (ex is AmazonS3Exception { StatusCode: HttpStatusCode.NotFound })
        {
            return Result<T>.NotFound("File not found.");
        }

        _logger.LogError(ex, "Storage {Operation} failed. Target={Target}", operation, target);

        int status = ex is AmazonServiceException
            ? StatusCodes.Status502BadGateway
            : StatusCodes.Status503ServiceUnavailable;

        return Result<T>.Failure("The file storage is temporarily unavailable.", status);
    }

    public void Dispose()
    {
        if (_ownsPresigner)
        {
            _presigner.Dispose();
        }
    }
}