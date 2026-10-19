using Microsoft.AspNetCore.Identity;

namespace Kairos.Api.Modules.Identity.Users.Models;

public class UserModel: IdentityUser
{
    public string? FullName { get; set; }
    public string[] Permissions { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiryTime { get; set; }
    
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}