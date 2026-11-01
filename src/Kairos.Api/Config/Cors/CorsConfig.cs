using Microsoft.Extensions.Options;
using AspNetCorsOptions =
    Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

namespace Kairos.Api.Config.Cors;

public static class CorsConfig
{
    public static IServiceCollection AddCorsConfig(
        this IServiceCollection services)
    {
        services.AddCors();

        services.AddSingleton<
            IConfigureOptions<AspNetCorsOptions>,
            CorsPolicyConfiguration>();

        return services;
    }
}