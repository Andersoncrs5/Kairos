using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Modules.Identity.Auth.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public class GetPrincipalFromExpiredTokenUseCase(
    IOptions<JwtOptions> jwtOptions)
    : IGetPrincipalFromExpiredTokenUseCase
{
    public ClaimsPrincipal Execute(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var options = jwtOptions.Value;

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = options.ValidAudience,

            ValidateIssuer = true,
            ValidIssuer = options.ValidIssuer,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(options.SecretKey)),

            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var principal = tokenHandler.ValidateToken(
            token,
            tokenValidationParameters,
            out var securityToken);

        if (securityToken is not JwtSecurityToken jwtSecurityToken ||
            !string.Equals(
                jwtSecurityToken.Header.Alg,
                SecurityAlgorithms.HmacSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token");
        }

        return principal;
    }
}