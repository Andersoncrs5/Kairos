using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.Reservation.Models;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

public interface IFindReservationByIdUseCase
    : IUseCase<string, Result<ReservationModel>>
{
}