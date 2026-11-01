using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.Version;

public static class ApiVersioningConfig
{
    public static IServiceCollection AddApiVersioningConfig(
        this IServiceCollection services)
    {
        services
            .AddApiVersioning()
            .AddMvc()
            .AddApiExplorer(_ => { });

        services.AddSingleton<
            IConfigureOptions<Asp.Versioning.ApiVersioningOptions>,
            ConfigureApiVersioningOptions>();

        services.AddSingleton<
            IConfigureOptions<ApiExplorerOptions>,
            ConfigureApiExplorerOptions>();

        return services;
    }
}