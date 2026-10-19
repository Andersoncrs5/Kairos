using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Models;

public class PermissionUserModel : BaseModel
{
    public required string UserId { get; set; }
    public required string PermissionId { get; set; }

    public bool IsActive { get; set; } = true;
}