using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.Models;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

public interface ICancelReservationUseCase
    : IUseCase<CancelReservationCommand, Result<ReservationModel>>
{
}