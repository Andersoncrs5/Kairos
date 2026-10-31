using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

public interface IExistsConflictingByRoomUseCase
{
    Task<Result<bool>> ExecuteAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken ct = default);
}