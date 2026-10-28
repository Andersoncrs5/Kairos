using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Kairos.Api.Config.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kairos.Api.Config.Authentication;

public static class AuthenticationConfig
{
    public static IServiceCollection AddJwtAuthenticationConfig(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>(
                (options, jwtOptionsAccessor) =>
                {
                    JwtOptions jwt = jwtOptionsAccessor.Value;

                    options.MapInboundClaims = false;

                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = jwt.ValidIssuer,

                            ValidateAudience = true,
                            ValidAudience = jwt.ValidAudience,

                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwt.SecretKey)),

                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.Zero,

                            NameClaimType = JwtRegisteredClaimNames.Name,
                            RoleClaimType = "role"
                        };
                });

        services.AddAuthorization();

        services.AddSingleton<
            IAuthorizationPolicyProvider,
            PermissionAuthorizationPolicyProvider>();

        services.AddScoped<
            IAuthorizationHandler,
            PermissionAuthorizationHandler>();

        return services;
    }
}