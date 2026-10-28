using Kairos.Api.Common.Base.Model;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Users.Models;

namespace Kairos.Api.Modules.Identity.PermissionUsers.Models;

public class PermissionUserModel : BaseModel
{
    public required string UserId { get; set; }
    public UserModel? User { get; set; }
    
    
    public required string PermissionId { get; set; }
    public PermissionsModel Permissions  { get; set; }
    
    
    public bool IsActive { get; set; } = true;
}