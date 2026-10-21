using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.Permissions.DTOs.Response;

public sealed class PermissionDto: BaseDTO
{

    public required string Code { get; init; }

    public required string Name { get; init; }

    public PermissionModuleEnum Module { get; init; }

    public bool IsSystem { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }
}