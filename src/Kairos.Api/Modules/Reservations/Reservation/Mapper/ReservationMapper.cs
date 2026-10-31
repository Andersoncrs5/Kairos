using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Riok.Mapperly.Abstractions;

namespace Kairos.Api.Modules.Reservations.Reservation.Mapper;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.Source,
    AllowNullPropertyAssignment = false)]
public static partial class ReservationMapper
{
    public static partial ReservationModel ToModel(
        CreateReservationDto source, string id, string userId, DateTimeOffset createdAt);

    [MapProperty(nameof(ReservationModel.Room), nameof(ReservationDto.RoomDto))]
    public static partial ReservationDto ToDto(ReservationModel source);

    public static partial void ApplyUpdate(UpdateReservationDto source, [MappingTarget] ReservationModel target);
}