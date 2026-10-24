namespace Kairos.Api.Modules.Identity.Auth.Models;


public class TokenResponse
{
    public required string Token { get; init; }
    public required DateTimeOffset Expired { get; init; }
}