using EFCoreSecondLevelCacheInterceptor;
using EntityFrameworkCore.Locking;
using EntityFrameworkCore.Locking.PostgreSQL;
using Kairos.Api.Config.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Kairos.Api.Config.DB;

public static class DependencyInjection
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // ----------------------------------------------------------
        // Connection strings
        // ----------------------------------------------------------

        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' não foi configurada.");

        string redisConnectionString =
            configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Connection string 'Redis' não foi configurada.");

        bool isTesting = environment.IsEnvironment("Testing");

        // ----------------------------------------------------------
        // 1. Redis
        // ----------------------------------------------------------

        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            RedisOptions settings = serviceProvider
                .GetRequiredService<IOptions<RedisOptions>>()
                .Value;

            ConfigurationOptions redisOptions =
                ConfigurationOptions.Parse(redisConnectionString);

            redisOptions.AbortOnConnectFail =
                settings.AbortOnConnectFail;

            redisOptions.ConnectRetry =
                settings.ConnectRetry;

            redisOptions.ConnectTimeout =
                settings.ConnectTimeoutMilliseconds;

            redisOptions.SyncTimeout =
                settings.SyncTimeoutMilliseconds;

            redisOptions.AsyncTimeout =
                settings.AsyncTimeoutMilliseconds;

            redisOptions.ClientName =
                settings.ClientName;

            return ConnectionMultiplexer.Connect(redisOptions);
        });

        // ----------------------------------------------------------
        // 2. EF Core Second Level Cache
        // ----------------------------------------------------------

        SecondLevelCacheOptions cacheSettings =
            configuration
                .GetSection(SecondLevelCacheOptions.SectionName)
                .Get<SecondLevelCacheOptions>()
            ?? new SecondLevelCacheOptions();

        services.AddEFSecondLevelCache(options =>
        {
            options
                .UseCacheKeyPrefix(cacheSettings.CacheKeyPrefix)
                .ConfigureLogging(environment.IsDevelopment())
                .UseDbCallsIfCachingProviderIsDown(
                    TimeSpan.FromMinutes(
                        cacheSettings.CacheProviderRetryMinutes))
                .CacheLockOptions(
                    cacheSettings.LockMode,
                    TimeSpan.FromSeconds(
                        cacheSettings.LockTimeoutSeconds));

            options.UseStackExchangeRedisCacheProvider(
                serviceProvider =>
                    serviceProvider.GetRequiredService<IConnectionMultiplexer>(),
                TimeSpan.FromMinutes(
                    cacheSettings.CacheExpirationMinutes));
        });

        // ----------------------------------------------------------
        // 3. AppDbContext
        // ----------------------------------------------------------

        services.AddDbContext<AppDbContext>(
            (serviceProvider, options) =>
            {
                PostgresOptions postgresSettings = serviceProvider
                    .GetRequiredService<IOptions<PostgresOptions>>()
                    .Value;

                options.UseNpgsql(
                    connectionString,
                    npgsql =>
                    {
                        npgsql.EnableRetryOnFailure(
                            maxRetryCount: postgresSettings.MaxRetryCount,
                            maxRetryDelay: TimeSpan.FromSeconds(
                                postgresSettings.MaxRetryDelaySeconds),
                            errorCodesToAdd: null);

                        npgsql.CommandTimeout(postgresSettings.CommandTimeoutSeconds);
                        
                        npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    });

                if (postgresSettings.EnableLocking)
                {
                    options.UseLocking();
                }

                options.LogTo(
                    Console.WriteLine,
                    LogLevel.Information);

                options.EnableDetailedErrors(
                    environment.IsDevelopment());

                if (environment.IsDevelopment() || isTesting)
                {
                    options.EnableSensitiveDataLogging();
                }

                options.AddInterceptors(
                    serviceProvider.GetRequiredService<
                        SecondLevelCacheInterceptor>());
            });

        return services;
    }
}