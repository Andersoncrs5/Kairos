using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Summary;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Repositories;

public interface IRoomFeedbackRepository
    : IBaseRepository<RoomFeedbackModel>
{
    Task<RoomFeedbackModel?> GetByIdWithDetailsAsync(
        string feedbackId,
        CancellationToken ct = default);

    Task<bool> ExistsByReservationAndUserAsync(
        string reservationId,
        string userId,
        CancellationToken ct = default);

    Task<RoomFeedbackRatingSummary> GetRatingSummaryByRoomIdAsync(
        string roomId,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, RoomRatingAverage>> GetRatingAveragesByRoomIdsAsync(
        IReadOnlyCollection<string> roomIds,
        CancellationToken ct = default);

    Task<Page<RoomFeedbackModel>> Find(
        FilterRoomFeedbackDto dto,
        CancellationToken ct = default);
}