using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;

public sealed class CancelRoomBlockoutDto
{
    [StringLength(
        255,
        ErrorMessage = "Cancellation reason cannot exceed 255 characters.")]
    public string? CancellationReason { get; init; }
}