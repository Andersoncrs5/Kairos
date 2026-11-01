using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class JwtAuthenticationOptionsConfig
{
    public static IServiceCollection AddJwtAuthenticationOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<JwtAuthenticationOptions>()
            .BindConfiguration(JwtAuthenticationOptions.SectionName)
            .Validate(
                options => options.ClockSkewSeconds >= 0,
                "JwtAuthentication:ClockSkewSeconds cannot be negative.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.NameClaimType),
                "JwtAuthentication:NameClaimType is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.RoleClaimType),
                "JwtAuthentication:RoleClaimType is required.")
            .ValidateOnStart();

        return services;
    }
}