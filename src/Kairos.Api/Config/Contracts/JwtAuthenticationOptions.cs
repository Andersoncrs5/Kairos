namespace Kairos.Api.Config.Contracts;

public sealed class JwtAuthenticationOptions
{
    public const string SectionName = "JwtAuthentication";

    public bool MapInboundClaims { get; set; } = false;

    public int ClockSkewSeconds { get; set; } = 0;

    public string NameClaimType { get; set; } = "name";

    public string RoleClaimType { get; set; } = "role";
}