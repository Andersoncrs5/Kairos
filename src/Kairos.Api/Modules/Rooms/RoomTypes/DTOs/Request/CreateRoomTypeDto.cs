using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;

public sealed class CreateRoomTypeDto
{
    [Required(ErrorMessage = "Room type name is required.")]
    [StringLength(
        100,
        ErrorMessage = "Room type name cannot exceed 100 characters.")]
    public required string Name { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Room type description cannot exceed 255 characters.")]
    public string? Description { get; init; }
}