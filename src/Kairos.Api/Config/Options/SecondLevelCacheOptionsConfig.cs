using EFCoreSecondLevelCacheInterceptor;
using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class SecondLevelCacheOptionsConfig
{
    public static IServiceCollection AddSecondLevelCacheOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<SecondLevelCacheOptions>()
            .BindConfiguration(SecondLevelCacheOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.CacheKeyPrefix),
                "SecondLevelCache:CacheKeyPrefix is required.")
            .Validate(
                options => options.CacheExpirationMinutes > 0,
                "SecondLevelCache:CacheExpirationMinutes must be greater than zero.")
            .Validate(
                options => options.CacheProviderRetryMinutes > 0,
                "SecondLevelCache:CacheProviderRetryMinutes must be greater than zero.")
            .Validate(
                options => options.LockTimeoutSeconds > 0,
                "SecondLevelCache:LockTimeoutSeconds must be greater than zero.")
            .Validate(
                options => Enum.IsDefined(options.LockMode),
                "SecondLevelCache:LockMode is invalid.")
            .ValidateOnStart();

        return services;
    }
}