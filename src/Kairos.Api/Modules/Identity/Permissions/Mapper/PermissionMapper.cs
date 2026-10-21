using Kairos.Api.Modules.Identity.Permissions.DTOs.Request;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Identity.Permissions.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class PermissionMapper
{
    public static partial PermissionsModel ToModel(
        CreatePermissionDto source,
        string Id,
        DateTimeOffset createdAt);

    public static partial PermissionDto ToDto(
        PermissionsModel source);

    public static partial void ApplyUpdate(
        UpdatePermissionDto source,
        [MappingTarget] PermissionsModel target);
}