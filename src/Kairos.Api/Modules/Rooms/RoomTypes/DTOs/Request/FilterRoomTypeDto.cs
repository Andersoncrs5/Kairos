using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;

public sealed class FilterRoomTypeDto : BaseFilter
{
    [StringLength(100, ErrorMessage = "Room type name cannot exceed 100 characters.")]
    public string? Name { get; init; }

    public bool? NameIgnoreCase { get; init; }

    [StringLength(255, ErrorMessage = "Room type description cannot exceed 255 characters.")]
    public string? Description { get; init; }

    public bool? DescriptionIgnoreCase { get; init; }

    public bool? IsActive { get; init; }
}