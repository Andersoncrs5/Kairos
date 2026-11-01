using EFCoreSecondLevelCacheInterceptor;
using EntityFrameworkCore.Locking.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Kairos.Api.Config.DB;

public static class DependencyInjection
{
    private static readonly TimeSpan CacheExpiration =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan CacheProviderRetry =
        TimeSpan.FromMinutes(1);

    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "A connection string 'DefaultConnection' não foi configurada.");

        string redisConnectionString =
            configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "A connection string 'Redis' não foi configurada.");

        bool isTesting = environment.IsEnvironment("Testing");

        // 1. Redis
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            ConfigurationOptions redisOptions =
                ConfigurationOptions.Parse(redisConnectionString);

            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 3;
            redisOptions.ConnectTimeout = 5000;
            redisOptions.SyncTimeout = 5000;
            redisOptions.AsyncTimeout = 5000;
            redisOptions.ClientName = "Kairos.Api";

            return ConnectionMultiplexer.Connect(redisOptions);
        });

        // 2. EF Core Second Level Cache (sempre Redis)
        services.AddEFSecondLevelCache(options =>
        {
            options
                .UseCacheKeyPrefix("Kairos_EF_")
                .ConfigureLogging(environment.IsDevelopment())
                .UseDbCallsIfCachingProviderIsDown(CacheProviderRetry)
                .CacheLockOptions(
                    EFLockMode.Keyed,
                    TimeSpan.FromSeconds(15));

            options.UseStackExchangeRedisCacheProvider(
                serviceProvider =>
                    serviceProvider.GetRequiredService<IConnectionMultiplexer>(),
                CacheExpiration);
        });

        // 3. AppDbContext
        services.AddDbContext<AppDbContext>(
            (serviceProvider, options) =>
            {
                options
                    .UseNpgsql(
                        connectionString,
                        npgsql =>
                        {
                            npgsql.EnableRetryOnFailure(
                                maxRetryCount: 5,
                                maxRetryDelay: TimeSpan.FromSeconds(7),
                                errorCodesToAdd: null);

                            npgsql.CommandTimeout(30);
                        })
                    .UseLocking()
                    .LogTo(Console.WriteLine, LogLevel.Information)
                    .EnableDetailedErrors(environment.IsDevelopment());

                if (environment.IsDevelopment() || isTesting)
                {
                    options.EnableSensitiveDataLogging();
                }

                options.AddInterceptors(
                    serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>());
            });

        return services;
    }
}