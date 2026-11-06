using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Base.Model;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;

public sealed class FilterRoomEquipmentDto : BaseFilter
{
    [StringLength(36, ErrorMessage = "Room ID must be a valid UUID.")]
    public string? RoomId { get; init; }
    
    [StringLength(30, ErrorMessage = "Room code cannot exceed 30 characters.")]
    public string? CodeRoom { get; init; }
    public bool? CodeRoomIgnoreCase { get; init; }

    [StringLength(120, ErrorMessage = "Room name cannot exceed 120 characters.")]
    public string? NameRoom { get; init; }
    public bool? NameRoomIgnoreCase { get; init; }

    
    [StringLength(36, ErrorMessage = "Equipment ID must be a valid UUID.")]
    public string? EquipmentId { get; init; }
    
    [StringLength(100, ErrorMessage = "Equipment name cannot exceed 100 characters.")]
    public string? Name { get; init; }
    public bool? NameIgnoreCase { get; init; }
    
    public int? QuantityMin { get; init; }

    public int? QuantityMax { get; init; }

    [StringLength(255, ErrorMessage = "Notes cannot exceed 255 characters.")]
    public string? Notes { get; init; }

    public bool? NotesIgnoreCase { get; init; }
}