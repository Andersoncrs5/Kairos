using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;


public sealed record DeletePermissionUserCommand(
    [property: Required(ErrorMessage = "UserId is required.")]
    [property: StringLength(450, MinimumLength = 1)]
    string UserId,

    [property: Required(ErrorMessage = "PermissionId is required.")]
    [property: StringLength(450, MinimumLength = 1)]
    string PermissionId);