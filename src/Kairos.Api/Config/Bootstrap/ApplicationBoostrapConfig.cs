using Kairos.Api.Common.Base.Seed;
using Kairos.Api.Modules.Identity.Permissions.Seed;
using Kairos.Api.Modules.Identity.PermissionUsers.Seed;
using Kairos.Api.Modules.Identity.Users.Seed;

namespace Kairos.Api.Config.Bootstrap;

public static class ApplicationBootstrapConfig
{
    public static IServiceCollection AddApplicationBootstrapConfig(
        this IServiceCollection services)
    {
        services.AddScoped<IPermissionSeeder, PermissionSeeder>();
        services.AddScoped<ISeedCase>(provider => provider.GetRequiredService<IPermissionSeeder>());

        services.AddScoped<IUserMasterSeed, UserMasterSeed>();
        services.AddScoped<ISeedCase>(provider => provider.GetRequiredService<IUserMasterSeed>());

        services.AddScoped<ILinkAllPermissionToUserMaster, LinkAllPermissionToUserMaster>();
        services.AddScoped<ISeedCase>(provider => provider.GetRequiredService<ILinkAllPermissionToUserMaster>());
        
        services.AddScoped<IApplicationBootstrapper, ApplicationBootstrapper>();

        return services;
    }
}