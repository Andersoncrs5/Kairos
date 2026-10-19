using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.Permissions.Models;

public class PermissionsModel: BaseModel
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public PermissionModuleEnum Module { get; set; } = PermissionModuleEnum.Identity;
    public bool IsSystem { get; set; } = false;
    public string? Description  { get; set; }
    public bool IsActive { get; set; } = true;
}