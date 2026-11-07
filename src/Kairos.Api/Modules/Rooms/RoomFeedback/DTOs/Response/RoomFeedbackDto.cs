using Kairos.Api.Common.Base.Model;
using Kairos.Api.Common.Enums;
using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;

public sealed class RoomFeedbackDto : BaseDTO
{
    public required string ReservationId { get; init; }
    public ReservationDto? Reservation { get; init; }

    public required string RoomId { get; init; }
    public RoomDto? Room { get; init; }

    public required string UserId { get; init; }
    public UserDTO? User { get; init; }

    public required int Rating { get; init; }

    public string? Comment { get; init; }

    public RoomFeedbackIssueTypeEnum? IssueType { get; init; }
}