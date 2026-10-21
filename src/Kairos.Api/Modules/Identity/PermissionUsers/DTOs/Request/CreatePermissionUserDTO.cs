using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;

public sealed class CreatePermissionUserDTO
{
    [Required]
    [StringLength(450, MinimumLength = 1)]
    public required string UserId { get; init; }

    [Required]
    [StringLength(450, MinimumLength = 1)]
    public required string PermissionId { get; init; }

    public bool IsActive { get; init; } = true;
}