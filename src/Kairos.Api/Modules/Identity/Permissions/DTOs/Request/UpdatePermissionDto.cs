using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.Permissions.DTOs.Request;

public sealed class UpdatePermissionDto
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }

    [StringLength(255)]
    public string? Description { get; set; }

    [EnumDataType(typeof(PermissionModuleEnum))]
    public PermissionModuleEnum? Module { get; set; }

    public bool? IsActive { get; set; }
}