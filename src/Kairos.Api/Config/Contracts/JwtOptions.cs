namespace Kairos.Api.Config.Contracts;

public class JwtOptions
{
    public required string SecretKey { get; set; } = string.Empty;
    public required string ValidIssuer { get; set; } = string.Empty;
    public required string ValidAudience { get; set; } = string.Empty;
    public required double TokenValidityInMinutes { get; set; }
    public required int RefreshTokenValidityInMinutes { get; set; }
}