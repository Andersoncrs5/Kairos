using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.Reservation.Models;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

public interface IGetConflictingByRoomUseCase
{
    Task<Result<IReadOnlyList<ReservationModel>>> ExecuteAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken ct = default);
}