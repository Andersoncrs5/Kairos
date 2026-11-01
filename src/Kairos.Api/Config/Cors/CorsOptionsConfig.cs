
using Kairos.Api.Config.Cors.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Kairos.Api.Config.Cors;

public static class CorsOptionsConfig
{
    public static IServiceCollection AddCorsOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<CorsOptions>()
            .BindConfiguration(CorsOptions.SectionName)
            .Validate(
                options =>
                    options.AllowedOrigins.Length > 0 &&
                    options.AllowedOrigins.All(IsValidOrigin),
                "At least one valid CORS origin must be configured.")
            .Validate(
                options =>
                    options.AllowedMethods.Length > 0 &&
                    options.AllowedMethods.All(
                        method => !string.IsNullOrWhiteSpace(method)),
                "At least one valid CORS method must be configured.")
            .Validate(
                options =>
                    options.AllowedHeaders.Length > 0 &&
                    options.AllowedHeaders.All(
                        header => !string.IsNullOrWhiteSpace(header)),
                "At least one valid CORS header must be configured.")
            .Validate(
                options =>
                    options.PreflightMaxAgeSeconds >= 0,
                "CORS preflight max age cannot be negative.")
            .Validate(
                options =>
                    !options.AllowCredentials ||
                    !options.AllowedOrigins.Contains("*"),
                "Wildcard origins cannot be used with credentials.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsValidOrigin(string origin)
    {
        if (!Uri.TryCreate(
                origin,
                UriKind.Absolute,
                out Uri? uri))
        {
            return false;
        }

        return
            (uri.Scheme == Uri.UriSchemeHttp ||
             uri.Scheme == Uri.UriSchemeHttps) &&
            uri.AbsolutePath == "/" &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment);
    }
}
