using Kairos.Api.Modules.Identity.Users.DTOs.Response;

namespace Kairos.Api.Modules.Identity.Auth.Models;

public class Tokens
{
    public required string Token { get; set; }
    public required DateTimeOffset ExpiredToken { get; set; }
    public required string RefreshToken { get; set; }
    public required UserDTO User { get; set; }
    public required string[] Permissions { get; set; }
}