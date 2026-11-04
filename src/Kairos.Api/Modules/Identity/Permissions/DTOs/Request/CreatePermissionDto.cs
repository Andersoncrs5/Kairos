using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.Permissions.DTOs.Request;

public sealed class CreatePermissionDto
{
    [Required(ErrorMessage = "Permission code is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Permission code must be between 1 and 100 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Permission code contains invalid characters.")]
    public required string Code { get; init; }

    [Required(ErrorMessage = "Permission name is required.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Permission name must be between 1 and 100 characters.")]
    public required string Name { get; init; }

    [Required(ErrorMessage = "Permission module is required.")]
    [EnumDataType(typeof(PermissionModuleEnum), ErrorMessage = "Invalid permission module.")]
    public PermissionModuleEnum Module { get; init; } = PermissionModuleEnum.Identity;

    [StringLength(255, ErrorMessage = "Description cannot exceed 255 characters.")]
    public string? Description { get; init; }
}