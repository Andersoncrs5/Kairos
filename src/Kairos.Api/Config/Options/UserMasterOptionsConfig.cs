using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class UserMasterOptionsConfig
{
    public static IServiceCollection AddUserMasterOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<UserMasterOptions>()
            .BindConfiguration("UserMaster")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}