using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;

public sealed record CancelRoomBlockoutCommand(
    [property: Required(
        ErrorMessage = "Room blockout ID is required.")]
    string RoomBlockoutId,

    [property: Required(
        ErrorMessage = "Room blockout cancellation reason is required.")]
    [property: StringLength(
        255,
        ErrorMessage = "Room blockout cancellation reason cannot exceed 255 characters.")]
    string CancellationReason,

    [property: Required(
        ErrorMessage = "Room blockout canceller ID is required.")]
    string CancelledBy
);