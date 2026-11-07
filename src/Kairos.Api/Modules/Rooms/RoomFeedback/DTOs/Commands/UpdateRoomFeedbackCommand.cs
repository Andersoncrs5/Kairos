using System.ComponentModel.DataAnnotations;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;

public sealed record UpdateRoomFeedbackCommand(
    [property: Required(ErrorMessage = "Room feedback ID is required.")]
    string FeedbackId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId,

    [property: Required(ErrorMessage = "Room feedback data is required.")]
    UpdateRoomFeedbackDto Dto
);