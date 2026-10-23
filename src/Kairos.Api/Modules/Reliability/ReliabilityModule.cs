using Kairos.Api.Modules.Reliability.Inbox.Repository;

namespace Kairos.Api.Modules.Reliability;

public static class ReliabilityModule
{
    public static IServiceCollection AddReliabilityModule(this IServiceCollection services)
    {
        services.AddScoped<IInboxRepository, InboxRepository>();
        
        return services;
    }
}