using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Permissions.DTOs.Response;
using Kairos.Api.Modules.Identity.Permissions.Models;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;

namespace Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;

public sealed class FilterPermissionUserDTO : BaseFilter
{
    [StringLength(450)]
    public string? UserId { get; init; }
    
    public string? UserName { get; init; }
    public bool? IgnoreCaseUserName { get; init; }
    
    public string? UserEmail { get; init; }
    public bool? IgnoreCaseUserEmail { get; init; }
    
    public UserDTO? User  { get; init; }

    
    [StringLength(450)]
    public string? PermissionId { get; init; }
    
    public string? PermissionCode { get; init; }
    public bool? IgnoreCasePermissionCode { get; init; }
    
    public string? PermissionName { get; init; }
    public bool? IgnoreCasePermissionName { get; init; }
    
    public PermissionModuleEnum[]? PermissionModuleEnums { get; init; }
    
    public PermissionDto Permission { get; init; }
    
    public bool? IsActive { get; init; }
}