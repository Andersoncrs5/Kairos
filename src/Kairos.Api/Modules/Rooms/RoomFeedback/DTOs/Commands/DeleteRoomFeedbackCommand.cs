using System.ComponentModel.DataAnnotations;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;

public sealed record DeleteRoomFeedbackCommand(
    [property: Required(ErrorMessage = "Room feedback ID is required.")]
    string FeedbackId,

    [property: Required(ErrorMessage = "Requester user ID is required.")]
    string RequesterUserId
);