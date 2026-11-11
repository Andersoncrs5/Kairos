using Amazon.S3;
using Amazon.S3.Model;
using Kairos.Api.Config.Storage.Contracts;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.Storage;

public sealed class StorageBucketInitializer(
    IAmazonS3 s3,
    IOptions<StorageOptions> options,
    ILogger<StorageBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        StorageOptions settings = options.Value;

        if (!settings.Enabled || !settings.EnsureBucketsOnStartup)
        {
            return;
        }

        const int attempts = 5;

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await EnsureBucketsAsync(settings, cancellationToken);
                return;
            }
            catch (Exception ex)
                when (ex is not OperationCanceledException && attempt < attempts)
            {
                logger.LogWarning(
                    ex,
                    "Storage not ready (attempt {Attempt}/{Attempts}). Retrying...",
                    attempt,
                    attempts);

                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(
                    ex,
                    "Could not ensure storage buckets. Upload endpoints will fail " +
                    "until the storage is reachable.");
                return;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureBucketsAsync(
        StorageOptions settings,
        CancellationToken cancellationToken)
    {
        ListBucketsResponse listed = await s3.ListBucketsAsync(cancellationToken);

        HashSet<string> existing = (listed.Buckets ?? [])
            .Select(bucket => bucket.BucketName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string bucket in settings.Buckets.All)
        {
            if (existing.Contains(bucket))
            {
                continue;
            }

            try
            {
                await s3.PutBucketAsync(
                    new PutBucketRequest { BucketName = bucket, UseClientRegion = true },
                    cancellationToken);

                logger.LogInformation("Storage bucket {Bucket} created.", bucket);
            }
            catch (AmazonS3Exception ex)
                when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                
            }
        }
    }
}