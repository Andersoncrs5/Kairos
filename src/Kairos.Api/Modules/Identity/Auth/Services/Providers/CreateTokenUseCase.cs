using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Contracts;
using Kairos.Api.Modules.Identity.Auth.Models;
using Kairos.Api.Modules.Identity.Users.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kairos.Api.Modules.Identity.Auth.Services.Providers;

public class CreateTokenUseCase(
    IOptions<JwtOptions> jwtOptions)
{
    public Task<Result<TokenResponse>> ExecuteAsync(UserModel user)
    {
        var permissions = user.Permissions;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name, user.UserName!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sid, user.Id),
        };

        claims.AddRange(
            permissions.Select(permission =>
                new Claim("permission", permission))
        );

        var options = jwtOptions.Value;

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(options.SecretKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var expires = DateTimeOffset.UtcNow
            .AddMinutes(options.TokenValidityInMinutes);

        var token = new JwtSecurityToken(
            issuer: options.ValidIssuer,
            audience: options.ValidAudience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var writtenToken = new JwtSecurityTokenHandler()
            .WriteToken(token);
        
        TokenResponse response = new TokenResponse
        {
            Token = writtenToken,
            Expired = expires
        };

        return Task.FromResult(
            Result<TokenResponse>.Success(response));
    }
}