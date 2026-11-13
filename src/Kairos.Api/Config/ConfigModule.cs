using Kairos.Api.Config.ApiBehavior;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.Bootstrap;
using Kairos.Api.Config.Cors;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Config.Health;
using Kairos.Api.Config.Http;
using Kairos.Api.Config.Options;
using Kairos.Api.Config.RateLimiting;
using Kairos.Api.Config.Resilience;
using Kairos.Api.Config.Storage;
using Kairos.Api.Config.Swagger;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Config.Version;

namespace Kairos.Api.Config;

public static class ConfigModule
{
    public static IServiceCollection AddConfigModule(
        this IServiceCollection services)
    {
        services.AddResilienceOptionsConfig();
        services.AddResilienceConfig();
        
        // API
        services.AddApiVersioningConfig();
        services.AddSwaggerConfig();

        // API Behavior
        services.AddApiBehaviorOptionsConfig();
        services.AddApiBehaviorConfig();

        // Bootstrap
        services.AddApplicationBootstrapConfig();

        // Options
        services.AddJwtOptionsConfig();
        services.AddJwtAuthenticationOptionsConfig();
        services.AddUserMasterOptionsConfig();

        services.AddPostgresOptionsConfig();
        services.AddRedisOptionsConfig();
        services.AddSecondLevelCacheOptionsConfig();

        // Authentication
        services.AddJwtAuthenticationConfig();
        
        // API Versioning
        services.AddApiVersioningOptionsConfig();
        services.AddApiVersioningConfig();
    
        services.AddObservabilityOptionsConfig();
        
        services.AddHealthChecksConfig();
        
        // CORS
        services.AddCorsOptionsConfig();
        services.AddCorsConfig();
        
        services.AddHttpHardeningConfig();
        
        services.AddOutputCacheConfig();
        
        services.AddRateLimitingOptionsConfig();
        services.AddRateLimitingConfig();
        
        services.AddStorageOptionsConfig();
        services.AddStorageConfig();
        
        // Transactions
        services.AddScoped(typeof(IInboxTransactionUseCase<,,>), typeof(InboxTransactionUseCase<,,>));
        
        services.AddScoped(
            typeof(ITransactionUseCase<,,>),
            typeof(TransactionUseCase<,,>));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        return services;
    }
}