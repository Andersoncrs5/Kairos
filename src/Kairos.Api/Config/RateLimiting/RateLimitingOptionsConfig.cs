using System.Reflection;
using Kairos.Api.Config.RateLimiting.Contracts;

namespace Kairos.Api.Config.RateLimiting;

public static class RateLimitingOptionsConfig
{
    public static IServiceCollection AddRateLimitingOptionsConfig(
        this IServiceCollection services)
    {
        var builder = services
            .AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .Validate(
                options => options.ExemptPaths.All(
                    path => !string.IsNullOrWhiteSpace(path) && path.StartsWith('/')),
                "RateLimiting:ExemptPaths must contain paths starting with '/'.");

        foreach (PropertyInfo property in typeof(RateLimitingOptions)
                     .GetProperties()
                     .Where(p => p.PropertyType == typeof(RateLimitRule)))
        {
            PropertyInfo captured = property;

            builder.Validate(
                options => ((RateLimitRule)captured.GetValue(options)!).IsValid,
                $"RateLimiting:{captured.Name} requires PermitLimit, " +
                "WindowSeconds and SegmentsPerWindow greater than zero.");
        }

        builder.ValidateOnStart();

        return services;
    }
}