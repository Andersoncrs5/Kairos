using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Auth.DTOs.Request;

public sealed class LoginUserDTO
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public required string Email { get; init; }

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must contain at least 8 characters.")]
    [StringLength(100, ErrorMessage = "Password cannot exceed 100 characters.")]
    public required string Password { get; init; }
}