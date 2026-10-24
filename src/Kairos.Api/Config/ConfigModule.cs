using Kairos.Api.Config.ApiBehavior;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Config.Swagger;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Config.Version;

namespace Kairos.Api.Config;

public static class ConfigModule
{
    public static IServiceCollection AddConfigModule(this IServiceCollection services)
    {
        services.AddApiVersioningConfig();
        services.AddSwaggerConfig();
        services.AddApiBehaviorConfig();

        services.AddScoped(typeof(IInboxTransactionUseCase<,,>), typeof(InboxTransactionUseCase<,,>));

        services.AddScoped(typeof(ITransactionUseCase<,,>), typeof(TransactionUseCase<,,>));

        services.AddScoped<
            IUnitOfWork,
            Kairos.Api.Config.UnitOfWork.UnitOfWork>();

        return services;
    }
}