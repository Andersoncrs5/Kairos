using ApplicationCorsOptions =
    Kairos.Api.Config.Cors.Contracts.CorsOptions;

using AspNetCorsOptions =
    Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions;

using Microsoft.Extensions.Options;

namespace Kairos.Api.Config.Cors;

public sealed class CorsPolicyConfiguration(
    IOptions<ApplicationCorsOptions> corsOptions)
    : IConfigureOptions<AspNetCorsOptions>
{
    public void Configure(AspNetCorsOptions options)
    {
        ApplicationCorsOptions settings = corsOptions.Value;

        options.AddPolicy(
            ApplicationCorsOptions.PolicyName,
            policy =>
            {
                policy
                    .WithOrigins(settings.AllowedOrigins)
                    .WithMethods(settings.AllowedMethods)
                    .WithHeaders(settings.AllowedHeaders);

                if (settings.ExposedHeaders.Length > 0)
                {
                    policy.WithExposedHeaders(
                        settings.ExposedHeaders);
                }

                if (settings.AllowCredentials)
                {
                    policy.AllowCredentials();
                }

                if (settings.PreflightMaxAgeSeconds > 0)
                {
                    policy.SetPreflightMaxAge(
                        TimeSpan.FromSeconds(
                            settings.PreflightMaxAgeSeconds));
                }
            });
    }
}