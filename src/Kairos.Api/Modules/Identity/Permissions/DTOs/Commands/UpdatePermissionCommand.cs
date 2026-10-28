using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;

namespace Kairos.Api.Modules.Identity.Permissions.DTOs.Commands;

public sealed record UpdatePermissionCommand(
    [property: Required(ErrorMessage = "Permission ID is required.")]
    string PermissionId,

    [property: Required(ErrorMessage = "Permission update data is required.")]
    UpdatePermissionDto Dto
);