using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Config.Contracts;

public sealed class UserMasterOptions
{
    [Required]
    [StringLength(256, MinimumLength = 3)]
    public required string UserName { get; init; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public required string Email { get; init; }

    [Required]
    [StringLength(150)]
    public required string FullName { get; init; }

    [Required]
    [MinLength(8)]
    public required string Password { get; init; }
}