using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;

public sealed class CreatePermissionUserDTO
{
    [Required(ErrorMessage = "UserId is required.")]
    [StringLength(450, MinimumLength = 1, ErrorMessage = "UserId must be between 1 and 450 characters.")]
    public required string UserId { get; init; }

    [Required(ErrorMessage = "PermissionId is required.")]
    [StringLength(450, MinimumLength = 1, ErrorMessage = "PermissionId must be between 1 and 450 characters.")]
    public required string PermissionId { get; init; }

    public bool IsActive { get; init; } = true;
}