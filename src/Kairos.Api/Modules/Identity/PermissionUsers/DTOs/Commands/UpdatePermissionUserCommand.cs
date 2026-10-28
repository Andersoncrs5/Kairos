using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;

namespace Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Commands;


public sealed record UpdatePermissionUserCommand(
    string Id,
    UpdatePermissionUserDTO? Dto);