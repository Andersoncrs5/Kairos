using Amazon.S3;
using Amazon.S3.Model;
using Kairos.Api.Config.Storage.Contracts;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.Health.Checks;

public sealed class StorageHealthCheck(
    IAmazonS3 s3,
    IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        StorageOptions settings = options.Value;

        if (!settings.Enabled)
        {
            return HealthCheckResult.Healthy("Storage is disabled.");
        }

        try
        {
            foreach (string bucket in settings.Buckets.All)
            {
                await s3.ListObjectsV2Async(
                    new ListObjectsV2Request { BucketName = bucket, MaxKeys = 1 },
                    cancellationToken);
            }

            return HealthCheckResult.Healthy("Storage is available.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Storage health check failed.");
        }
    }
}