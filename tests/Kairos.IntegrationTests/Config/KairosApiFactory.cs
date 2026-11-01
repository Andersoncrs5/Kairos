using EFCoreSecondLevelCacheInterceptor;
using EntityFrameworkCore.Locking.PostgreSQL;
using Kairos.Api.Config.DB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace Kairos.IntegrationTests.Config;

public sealed class KairosApiFactory
    : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("kairos_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string ConnectionString =>
        _dbContainer.GetConnectionString();

    public string RedisConnectionString =>
        _redisContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Redis do container no lugar do localhost:6379
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                ConfigurationOptions options =
                    ConfigurationOptions.Parse(_redisContainer.GetConnectionString());

                options.AbortOnConnectFail = false;
                options.ClientName = "Kairos.IntegrationTests";

                return ConnectionMultiplexer.Connect(options);
            });

            // DbContext apontando para o Postgres do container,
            // mantendo o interceptor do cache
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            {
                options
                    .UseNpgsql(_dbContainer.GetConnectionString())
                    .UseLocking()
                    .AddInterceptors(serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>());
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Sobe os dois containers em paralelo
        await Task.WhenAll(
            _dbContainer.StartAsync(),
            _redisContainer.StartAsync());

        try
        {
            using var scope = Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            await db.Database.EnsureCreatedAsync();
        }
        catch
        {
            await DisposeContainersAsync();
            throw;
        }
    }

    /// <summary>
    /// Limpa o cache entre testes para evitar resultado "velho"
    /// quando o banco é resetado ou alterado fora da API.
    /// </summary>
    public async Task FlushCacheAsync()
    {
        await _redisContainer.ExecAsync(new[] { "redis-cli", "FLUSHALL" });
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await DisposeContainersAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    private async Task DisposeContainersAsync()
    {
        await _dbContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}