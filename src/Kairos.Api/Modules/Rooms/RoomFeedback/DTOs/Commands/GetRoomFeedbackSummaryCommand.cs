using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;

public sealed record GetRoomFeedbackSummaryCommand(
    [property: Required(ErrorMessage = "Room ID is required.")]
    string RoomId
);