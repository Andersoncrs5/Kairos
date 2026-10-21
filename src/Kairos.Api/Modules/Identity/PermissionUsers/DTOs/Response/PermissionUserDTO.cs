using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Response;

public sealed class PermissionUserDTO : BaseDTO
{
    public required string UserId { get; init; }

    public required string PermissionId { get; init; }
    
    public bool IsActive { get; init; }
}