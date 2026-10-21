using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;

namespace Kairos.Api.Modules.Identity.Permissions.DTOs.Request;

public sealed class PermissionFilterDto : BaseFilter
{
    [StringLength(100)]
    public string? Code { get; set; }
    public bool? CodeIgnoreCase { get; set; }

    [StringLength(100)] public string? Name { get; set; }
    public bool? NameIgnoreCase { get; set; }

    [EnumDataType(typeof(PermissionModuleEnum))]
    public List<PermissionModuleEnum>? Modules { get; set; }

    public bool? IsSystem { get; set; }

    public bool? IsActive { get; set; }
}