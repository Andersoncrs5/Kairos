using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;

public sealed class UpdateRoomEquipmentDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int? Quantity { get; init; }

    [StringLength(255, ErrorMessage = "Notes cannot exceed 255 characters.")]
    public string? Notes { get; init; }
}