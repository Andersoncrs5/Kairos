using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Identity.PermissionUsers.DTOs.Request;

public sealed class FilterPermissionUserDTO : BaseFilter
{
    [StringLength(450)]
    public string? UserId { get; init; }

    [StringLength(450)]
    public string? PermissionId { get; init; }

    public bool? IsActive { get; init; }
}