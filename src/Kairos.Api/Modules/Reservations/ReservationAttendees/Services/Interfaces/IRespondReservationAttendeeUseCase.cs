using Kairos.Api.Common.Base.Services;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;

public interface IRespondReservationAttendeeUseCase
    : IUseCase<RespondReservationAttendeeCommand, Result<ReservationAttendeeModel>>
{
}