using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using MvcApiBehaviorOptions =
    Microsoft.AspNetCore.Mvc.ApiBehaviorOptions;

namespace Kairos.Api.Config.ApiBehavior;

public static class ApiBehaviorConfig
{
    public static IServiceCollection AddApiBehaviorConfig(
        this IServiceCollection services)
    {
        services.AddControllers();

        services.AddSingleton<
            IConfigureOptions<MvcApiBehaviorOptions>,
            ConfigureMvcApiBehaviorOptions>();

        return services;
    }
}