using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.Permissions.DTOs.Request;

public sealed class UpdatePermissionDto
{
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Permission name must be between 1 and 100 characters.")]
    public string? Name { get; init; }

    [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
    public string? Description { get; init; }

    [EnumDataType(typeof(PermissionModuleEnum), ErrorMessage = "Invalid permission module.")]
    public PermissionModuleEnum? Module { get; init; }

    public bool? IsActive { get; init; }
}