using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

public interface IExistsConflictingByRoomUseCase
    : IUseCase<ReservationRoomConflictCommand, Result<bool>>
{
}