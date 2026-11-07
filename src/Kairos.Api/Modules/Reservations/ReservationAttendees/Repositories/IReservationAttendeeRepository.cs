using Kairos.Api.Common.Base.Repository;
using Kairos.Api.Common.Page;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Repositories;

public interface IReservationAttendeeRepository
    : IBaseRepository<ReservationAttendeeModel>
{
    Task<ReservationAttendeeModel?> GetByReservationAndUserAsync(
        string reservationId,
        string userId,
        CancellationToken ct = default);

    Task<ReservationAttendeeModel?> GetByIdAndReservationAsync(
        string attendeeId,
        string reservationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReservationAttendeeModel>> GetByReservationIdAsync(
        string reservationId,
        CancellationToken ct = default);

    Task<bool> ExistsByReservationAndUserAsync(
        string reservationId,
        string userId,
        CancellationToken ct = default);

    Task<int> CountOccupyingByReservationAsync(
        string reservationId,
        CancellationToken ct = default);

    Task<Page<ReservationAttendeeModel>> Find(
        FilterReservationAttendeeDto dto,
        CancellationToken ct = default);
}