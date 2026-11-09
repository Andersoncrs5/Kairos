using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.Mapper;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.Mapper;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;
using Kairos.Api.Modules.Rooms.RoomFeedback.Summary;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class RoomFeedbackMapper
{
    public static partial RoomFeedbackDto ToDto(RoomFeedbackModel source);

    public static RoomFeedbackModel ToModel(
        CreateRoomFeedbackDto source,
        string id,
        string reservationId,
        string roomId,
        string userId,
        DateTimeOffset createdAt)
    {
        return new RoomFeedbackModel
        {
            Id = id,
            ReservationId = reservationId,
            RoomId = roomId,
            UserId = userId,
            Rating = source.Rating,
            Comment = NormalizeComment(source.Comment),
            IssueType = source.IssueType,
            Version = 1,
            CreatedAt = createdAt,
            UpdatedAt = null
        };
    }

    public static void ApplyUpdate(
        UpdateRoomFeedbackDto source,
        RoomFeedbackModel target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.Rating = source.Rating;
        target.Comment = NormalizeComment(source.Comment);
        target.IssueType = source.IssueType;
    }

    public static RoomFeedbackSummaryDto ToSummaryDto(
        string roomId,
        RoomFeedbackRatingSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new RoomFeedbackSummaryDto
        {
            RoomId = roomId,
            TotalCount = summary.TotalCount,
            AverageRating = summary.AverageRating.HasValue
                ? Math.Round(
                    summary.AverageRating.Value,
                    2,
                    MidpointRounding.AwayFromZero)
                : null,
            RatingDistribution = summary.RatingDistribution
        };
    }
    
    public static string? NormalizeComment(string? comment)
        => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    
    private static UserDTO MapUser(UserModel source)
        => UserMapper.ToDto(source);

    private static RoomDto MapRoom(RoomModel source)
        => RoomMapper.ToDto(source);

    private static ReservationDto MapReservation(ReservationModel source)
        => ReservationMapper.ToDto(source);
}