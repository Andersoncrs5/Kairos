using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;

public sealed class AddEquipmentToRoomDto
{
    [Required(ErrorMessage = "Equipment ID is required.")]
    [StringLength(36, ErrorMessage = "Equipment ID must be a valid UUID.")]
    public required string EquipmentId { get; init; }
    
    [Required(ErrorMessage = "Room ID is required.")]
    [StringLength(36, ErrorMessage = "Room ID must be a valid UUID.")]
    public required string RoomId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; init; } = 1;

    [StringLength(255, ErrorMessage = "Notes cannot exceed 255 characters.")]
    public string? Notes { get; init; }
}