using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;

public sealed record CreateRoomFeedbackCommand(
    [property: Required(ErrorMessage = "Reservation ID is required.")]
    string ReservationId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Room feedback data is required.")]
    CreateRoomFeedbackDto Dto
);