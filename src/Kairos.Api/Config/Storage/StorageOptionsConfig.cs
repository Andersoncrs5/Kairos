using System.Text.RegularExpressions;
using Kairos.Api.Config.Storage.Contracts;

namespace Kairos.Api.Config.Storage;

public static partial class StorageOptionsConfig
{
    public static IServiceCollection AddStorageOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .Validate(
                options => IsHttpUrl(options.ServiceUrl),
                "Storage:ServiceUrl must be an absolute http/https URL.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.PublicUrl)
                           || IsHttpUrl(options.PublicUrl),
                "Storage:PublicUrl must be an absolute http/https URL.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Region),
                "Storage:Region is required.")
            .Validate(
                options => !options.Enabled
                           || (!string.IsNullOrWhiteSpace(options.AccessKey)
                               && !string.IsNullOrWhiteSpace(options.SecretKey)),
                "Storage:AccessKey and Storage:SecretKey are required " +
                "(use user-secrets or environment variables).")
            .Validate(
                options => options.Buckets.All.All(
                    name => BucketNameRegex.IsMatch(name)),
                "Storage:Buckets must follow the S3 bucket naming rules " +
                "(3-63 chars: lowercase letters, digits, '.' and '-').")
            .Validate(
                options => options.Buckets.All.Distinct().Count()
                           == options.Buckets.All.Count(),
                "Storage:Buckets must have distinct names.")
            .Validate(
                options => options.PresignedUrlExpirationMinutes is >= 1 and <= 10080,
                "Storage:PresignedUrlExpirationMinutes must be between 1 and 10080.")
            .Validate(
                options => options.MaxErrorRetry >= 0 && options.TimeoutSeconds > 0,
                "Storage:MaxErrorRetry cannot be negative and TimeoutSeconds must be positive.")
            .Validate(
                options => options.Limits.MaxPhotoBytes > 0
                           && options.Limits.MaxFloorPlanBytes > 0
                           && options.Limits.MaxSupportMaterialBytes > 0,
                "Storage:Limits must be greater than zero.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static readonly Regex BucketNameRegex = new(
        "^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
}