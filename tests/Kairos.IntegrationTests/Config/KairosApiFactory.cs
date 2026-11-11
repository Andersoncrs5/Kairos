
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

using EFCoreSecondLevelCacheInterceptor;
using EntityFrameworkCore.Locking.PostgreSQL;

using Kairos.Api.Config.DB;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    // PostgreSQL
    private readonly PostgreSqlContainer _dbContainer =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("kairos_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    // Redis
    private readonly RedisContainer _redisContainer =
        new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

    // RustFS / S3
    private const ushort S3Port = 9000;

    public const string S3AccessKey = "kairos-test-access";
    public const string S3SecretKey = "kairos-test-secret";

    public const string RoomMediaBucketName = "kairos-room-media";
    public const string SupportMaterialsBucketName = "kairos-support-materials";

    private readonly IContainer _s3Container =
        new ContainerBuilder("rustfs/rustfs:latest")
            .WithPortBinding(S3Port, true)
            .WithEnvironment("RUSTFS_ACCESS_KEY", S3AccessKey)
            .WithEnvironment("RUSTFS_SECRET_KEY", S3SecretKey)
            .WithEnvironment("RUSTFS_ADDRESS", "0.0.0.0:9000")
            .WithEnvironment("RUSTFS_CONSOLE_ENABLE", "false")
            .WithCommand("/data")
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request
                        .ForPort(S3Port)
                        .ForPath("/health")))
            .Build();

    // Connection strings
    public string ConnectionString =>
        _dbContainer.GetConnectionString();

    public string RedisConnectionString =>
        _redisContainer.GetConnectionString();

    public string S3Endpoint =>
        $"http://{_s3Container.Hostname}:{_s3Container.GetMappedPublicPort(S3Port)}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Sobrescreve as configurações do Storage para utilizar o RustFS.
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            Dictionary<string, string?> storageSettings = new()
            {
                ["Storage:Enabled"] = "true",
                ["Storage:ServiceUrl"] = S3Endpoint,
                ["Storage:Region"] = "us-east-1",
                ["Storage:AccessKey"] = S3AccessKey,
                ["Storage:SecretKey"] = S3SecretKey,
                ["Storage:ForcePathStyle"] = "true",
                ["Storage:EnsureBucketsOnStartup"] = "true",

                ["Storage:Buckets:RoomMedia"] = RoomMediaBucketName,
                ["Storage:Buckets:SupportMaterials"] = SupportMaterialsBucketName
            };

            configuration.AddInMemoryCollection(storageSettings);
        });

        builder.ConfigureTestServices(services =>
        {
            // RustFS no lugar do AWS S3.
            services.RemoveAll<IAmazonS3>();
            services.AddSingleton<IAmazonS3>(_ => CreateS3Client());

            // Redis do container.
            services.RemoveAll<IConnectionMultiplexer>();

            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                ConfigurationOptions options =
                    ConfigurationOptions.Parse(RedisConnectionString);

                options.AbortOnConnectFail = false;
                options.ClientName = "Kairos.IntegrationTests";

                return ConnectionMultiplexer.Connect(options);
            });

            // PostgreSQL do container, mantendo o interceptor do cache.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            {
                options
                    .UseNpgsql(ConnectionString)
                    .UseLocking()
                    .AddInterceptors(
                        serviceProvider.GetRequiredService<SecondLevelCacheInterceptor>());
            });
        });
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Inicializa os três containers em paralelo.
            await Task.WhenAll(
                _dbContainer.StartAsync(),
                _redisContainer.StartAsync(),
                _s3Container.StartAsync());

            // Cria os buckets no RustFS.
            await InitializeS3Async();

            // Inicializa o host e o banco de dados.
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
    /// Limpa o cache entre testes para evitar resultados antigos.
    /// </summary>
    public async Task FlushCacheAsync()
    {
        await _redisContainer.ExecAsync(
            ["redis-cli", "FLUSHALL"]);
    }

    // Descarte do WebApplicationFactory e dos containers.
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
        await _s3Container.DisposeAsync();
    }

    // AWS SDK configurado para utilizar o endpoint do RustFS.
    private AmazonS3Client CreateS3Client()
    {
        AmazonS3Config configuration = new()
        {
            ServiceURL = S3Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };

        BasicAWSCredentials credentials = new(
            S3AccessKey,
            S3SecretKey);

        return new AmazonS3Client(credentials, configuration);
    }

    // Cria os buckets utilizados pela aplicação.
    private async Task InitializeS3Async()
    {
        using AmazonS3Client client = CreateS3Client();

        string[] buckets =
        [
            RoomMediaBucketName,
            SupportMaterialsBucketName
        ];

        foreach (string bucket in buckets)
        {
            await client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucket
            });
        }
    }
}
