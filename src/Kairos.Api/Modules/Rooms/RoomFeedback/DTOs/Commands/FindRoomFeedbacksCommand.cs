using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;

public sealed record FindRoomFeedbacksCommand(
    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Room feedback filter is required.")]
    FilterRoomFeedbackDto Dto
);