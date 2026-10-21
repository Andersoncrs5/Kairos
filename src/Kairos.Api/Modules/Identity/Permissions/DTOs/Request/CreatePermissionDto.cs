using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.Permissions.DTOs.Request;

public sealed class CreatePermissionDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Code { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; set; }

    [EnumDataType(typeof(PermissionModuleEnum))]
    public PermissionModuleEnum Module { get; set; }
        = PermissionModuleEnum.Identity;

    [StringLength(255)]
    public string? Description { get; set; }
}