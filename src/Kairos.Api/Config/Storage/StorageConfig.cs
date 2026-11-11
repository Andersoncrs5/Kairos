using Amazon.Runtime;
using Amazon.S3;
using Kairos.Api.Config.Storage.Contracts;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.Storage;

public static class StorageConfig
{
    public static IServiceCollection AddStorageConfig(
        this IServiceCollection services)
    {
        services.AddSingleton<IAmazonS3>(provider =>
        {
            StorageOptions options =
                provider.GetRequiredService<IOptions<StorageOptions>>().Value;

            return S3ClientFactory.Create(options, options.ServiceUrl);
        });

        services.AddSingleton<IFileStorageService, S3FileStorageService>();

        services.AddHostedService<StorageBucketInitializer>();

        return services;
    }
}

internal static class S3ClientFactory
{
    public static AmazonS3Client Create(StorageOptions options, string serviceUrl)
    {
        var config = new AmazonS3Config
        {
            AuthenticationRegion = options.Region,
            ServiceURL = serviceUrl,
            ForcePathStyle = options.ForcePathStyle,

            MaxErrorRetry = options.MaxErrorRetry,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),

            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        string accessKey = string.IsNullOrWhiteSpace(options.AccessKey) ? "not-configured" : options.AccessKey;

        string secretKey = string.IsNullOrWhiteSpace(options.SecretKey)
            ? "not-configured"
            : options.SecretKey;

        return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
    }
}