using Kairos.Api.Modules.Reliability.Inbox.Repository;
using Kairos.Api.Modules.Reliability.Inbox.Service.Interface;
using Kairos.Api.Modules.Reliability.Inbox.Service.provider;

namespace Kairos.Api.Modules.Reliability;

public static class ReliabilityModule
{
    public static IServiceCollection AddReliabilityModule(this IServiceCollection services)
    {
        services.AddScoped<IInboxRepository, InboxRepository>();
        services.AddScoped<ICreateInboxUseCase, CreateInboxUseCase>();
        
        return services;
    }
}