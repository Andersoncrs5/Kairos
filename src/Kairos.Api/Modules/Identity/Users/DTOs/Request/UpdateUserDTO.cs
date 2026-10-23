using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.Users.DTOs.Request;

public sealed class UpdateUserDTO
{
    [StringLength(150)]
    public string? FullName { get; init; }

    [StringLength(256, MinimumLength = 3)]
    public string? UserName { get; init; }

    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; init; }

    [Phone]
    [StringLength(30)]
    public string? PhoneNumber { get; init; }
}