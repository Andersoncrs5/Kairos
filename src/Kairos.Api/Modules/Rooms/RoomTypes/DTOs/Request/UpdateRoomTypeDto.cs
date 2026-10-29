using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;

public sealed class UpdateRoomTypeDto
{
    [StringLength(
        100,
        ErrorMessage = "Room type name cannot exceed 100 characters.")]
    public string? Name { get; init; }

    [StringLength(
        255,
        ErrorMessage = "Room type description cannot exceed 255 characters.")]
    public string? Description { get; init; }

    public bool? IsActive { get; init; }
}