namespace Kairos.Api.Modules.Identity.Users.DTOs.Response;

public class UserDTO
{
    public required string Id { get; set; }

    public string? FullName { get; set; }

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string[] Permissions { get; set; } = [];

    public required DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}