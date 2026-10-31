using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.Models;

namespace Kairos.Api.Modules.Reservations.Reservation.Repositories;

public interface IReservationRepository: IBaseRepository<ReservationModel>
{
    Task<ReservationModel?> GetByIdempotencyKeyAsync(string userId, string idempotencyKey, CancellationToken ct = default);

    Task<IReadOnlyList<ReservationModel>> GetByUserIdAsync(string userId, CancellationToken ct = default);

    Task<IReadOnlyList<ReservationModel>> GetConflictingByRoomAsync(string roomId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct = default);
    
    Task<bool> ExistsConflictingByRoomAsync(string roomId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct = default);
    
    Task<bool> ExistsConflictingByRoomAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string excludeReservationId,
        CancellationToken ct = default);
    
    Task<IReadOnlyList<ReservationModel>> GetUpcomingByUserIdAsync(string userId, DateTimeOffset from, int take = 10, CancellationToken ct = default);
    
    Task<Page<ReservationModel>> Find(FilterReservationDto dto, CancellationToken ct = default);
}