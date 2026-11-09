using Kairos.Api.Modules.Identity.UserPreference.DTOs.Responses;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;

namespace Kairos.Api.Modules.Identity.Auth.DTOs.Response;

public class Tokens
{
    public required string Token { get; set; }
    public required DateTimeOffset ExpiredToken { get; set; }
    public required string RefreshToken { get; set; }
    public required UserDTO User { get; set; }
    public required string[] Permissions { get; set; }
    public required UserPreferenceDTO Preference { get; set; }
}