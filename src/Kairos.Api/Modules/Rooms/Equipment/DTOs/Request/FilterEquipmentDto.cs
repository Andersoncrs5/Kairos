using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;

public sealed class FilterEquipmentDto : BaseFilter
{
    [StringLength(50, ErrorMessage = "Equipment code cannot exceed 50 characters.")]
    public string? Code { get; init; }

    [StringLength(100, ErrorMessage = "Equipment name cannot exceed 100 characters.")]
    public string? Name { get; init; }

    public bool? NameIgnoreCase { get; init; }

    [StringLength(255, ErrorMessage = "Equipment description cannot exceed 255 characters.")]
    public string? Description { get; init; }

    public bool? IsActive { get; init; }
}   