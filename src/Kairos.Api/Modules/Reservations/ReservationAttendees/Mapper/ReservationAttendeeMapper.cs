using Kairos.Api.Modules.Identity.Users.DTOs.Response;
using Kairos.Api.Modules.Identity.Users.Mapper;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.Mapper;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Response;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class ReservationAttendeeMapper
{
    public static partial ReservationAttendeeDto ToDto(
        ReservationAttendeeModel source);

    private static UserDTO MapUser(UserModel source)
        => UserMapper.ToDto(source);

    private static ReservationDto MapReservation(ReservationModel source)
        => ReservationMapper.ToDto(source);
}