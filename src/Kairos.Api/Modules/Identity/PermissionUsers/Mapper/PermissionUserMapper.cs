using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;
using Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Response;
using Kairos.Api.Modules.Identity.PermissionUsers.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Mapper;

[Mapper(AllowNullPropertyAssignment = false)]
public static partial class PermissionUserMapper
{
    public static partial PermissionUserModel ToModel(
        CreatePermissionUserDTO source,
        string id,
        DateTimeOffset createdAt);

    public static partial PermissionUserDTO ToDto(
        PermissionUserModel source);

    public static partial void ApplyUpdate(
        UpdatePermissionUserDTO source,
        [MappingTarget] PermissionUserModel target);
}