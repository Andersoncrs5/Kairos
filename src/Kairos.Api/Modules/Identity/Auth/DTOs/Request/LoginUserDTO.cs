namespace Kairos.Api.Modules.Identity.Auth.DTOs.Request;

public class LoginUserDTO
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}