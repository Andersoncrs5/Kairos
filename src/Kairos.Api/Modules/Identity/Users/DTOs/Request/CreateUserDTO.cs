using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Users.DTOs.Request;

public sealed class CreateUserDTO
{
    [StringLength(150)]
    public string? FullName { get; init; }

    [Required]
    [StringLength(256, MinimumLength = 3)]
    public required string UserName { get; init; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public required string Email { get; init; }

    [Phone]
    [StringLength(30)]
    public string? PhoneNumber { get; init; }

    [Required]
    [MinLength(8)]
    public required string Password { get; init; }
}