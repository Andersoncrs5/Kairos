using Kairos.Api.Config.UnitOfWork;

namespace Kairos.Api.Config;

public static class ConfigModule
{
    public static IServiceCollection AddConfigModule(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        return services;
    }
}