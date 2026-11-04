using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Users.DTOs.Request;

public sealed class UpdateUserDTO
{
    [StringLength(150, ErrorMessage = "FullName cannot exceed 150 characters.")]
    public string? FullName { get; init; }

    [StringLength(256, MinimumLength = 3, ErrorMessage = "UserName must be between 3 and 256 characters.")]
    public string? UserName { get; init; }

    [EmailAddress(ErrorMessage = "Email must be a valid email address.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public string? Email { get; init; }

    [Phone(ErrorMessage = "PhoneNumber is invalid.")]
    [StringLength(30, ErrorMessage = "PhoneNumber cannot exceed 30 characters.")]
    public string? PhoneNumber { get; init; }
}