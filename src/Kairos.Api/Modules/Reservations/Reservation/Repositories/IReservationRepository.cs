using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Modules.Reservations.Reservation.Models;

namespace Kairos.Api.Modules.Reservations.Reservation.Repositories;

public interface IReservationRepository
    : IBaseRepository<ReservationModel>
{
    Task<ReservationModel?> GetByIdempotencyKeyAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationModel>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReservationModel>> GetConflictingByRoomAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken cancellationToken = default);
}