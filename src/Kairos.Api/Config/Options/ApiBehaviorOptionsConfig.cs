using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class ApiBehaviorOptionsConfig
{
    public static IServiceCollection AddApiBehaviorOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<ApiBehaviorOptions>()
            .BindConfiguration(ApiBehaviorOptions.SectionName)
            .ValidateOnStart();

        return services;
    }
}