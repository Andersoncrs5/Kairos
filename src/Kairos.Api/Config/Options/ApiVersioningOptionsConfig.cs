using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class ApiVersioningOptionsConfig
{
    public static IServiceCollection AddApiVersioningOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<ApiVersioningOptions>()
            .BindConfiguration(ApiVersioningOptions.SectionName)
            .Validate(
                options => options.DefaultMajorVersion > 0,
                "ApiVersioning:DefaultMajorVersion must be greater than zero.")
            .Validate(
                options => options.DefaultMinorVersion >= 0,
                "ApiVersioning:DefaultMinorVersion cannot be negative.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.GroupNameFormat),
                "ApiVersioning:GroupNameFormat is required.")
            .ValidateOnStart();

        return services;
    }
}