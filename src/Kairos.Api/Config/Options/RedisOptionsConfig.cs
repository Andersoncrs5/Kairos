using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class RedisOptionsConfig
{
    public static IServiceCollection AddRedisOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<RedisOptions>()
            .BindConfiguration(RedisOptions.SectionName)
            .Validate(
                options => options.ConnectRetry >= 0,
                "Redis:ConnectRetry cannot be negative.")
            .Validate(
                options => options.ConnectTimeoutMilliseconds > 0,
                "Redis:ConnectTimeoutMilliseconds must be greater than zero.")
            .Validate(
                options => options.SyncTimeoutMilliseconds > 0,
                "Redis:SyncTimeoutMilliseconds must be greater than zero.")
            .Validate(
                options => options.AsyncTimeoutMilliseconds > 0,
                "Redis:AsyncTimeoutMilliseconds must be greater than zero.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ClientName),
                "Redis:ClientName is required.")
            .ValidateOnStart();

        return services;
    }
}