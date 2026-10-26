using System.Text;
using Kairos.Api.Config.Contracts;

namespace Kairos.Api.Config.Options;

public static class JwtOptionsConfig
{
    public static IServiceCollection AddJwtOptionsConfig(
        this IServiceCollection services)
    {
        services
            .AddOptions<JwtOptions>()
            .BindConfiguration("Jwt")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.SecretKey) &&
                    Encoding.UTF8.GetByteCount(options.SecretKey) >= 32,
                "Jwt:SecretKey must contain at least 32 bytes.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ValidIssuer),
                "Jwt:ValidIssuer is required.")
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.ValidAudience),
                "Jwt:ValidAudience is required.")
            .Validate(
                options =>
                    double.IsFinite(options.TokenValidityInMinutes) &&
                    options.TokenValidityInMinutes > 0,
                "Jwt:TokenValidityInMinutes must be greater than zero.")
            .Validate(
                options => options.RefreshTokenValidityInMinutes > 0,
                "Jwt:RefreshTokenValidityInMinutes must be greater than zero.")
            .ValidateOnStart();

        return services;
    }
}