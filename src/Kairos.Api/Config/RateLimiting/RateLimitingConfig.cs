using Kairos.Api.Config.RateLimiting.Contracts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.RateLimiting;

public static class RateLimitingConfig
{
    public static IServiceCollection AddRateLimitingConfig(
        this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });

        services.AddSingleton<
            IConfigureOptions<RateLimiterOptions>,
            RateLimitPolicyConfiguration>();

        return services;
    }

    public static WebApplication UseRateLimitingConfig(this WebApplication app)
    {
        RateLimitingOptions settings = app.Services
            .GetRequiredService<IOptions<RateLimitingOptions>>()
            .Value;

        if (settings.Enabled)
        {
            app.UseRateLimiter();
        }

        return app;
    }
}