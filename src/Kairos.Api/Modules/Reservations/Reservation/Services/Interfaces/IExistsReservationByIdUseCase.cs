using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

public interface IExistsReservationByIdUseCase
    : IUseCase<string, Result<bool>>
{
}