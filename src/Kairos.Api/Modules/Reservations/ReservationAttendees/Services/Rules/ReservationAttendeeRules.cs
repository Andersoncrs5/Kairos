using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;

internal static class ReservationAttendeeRules
{
    public static bool TryValidate(
        object instance,
        string fallbackMessage,
        out string[] errors)
    {
        List<ValidationResult> results = [];

        if (Validator.TryValidateObject(
                instance,
                new ValidationContext(instance),
                results,
                validateAllProperties: true))
        {
            errors = [];
            return true;
        }

        errors = results
            .Select(result => result.ErrorMessage ?? fallbackMessage)
            .Distinct()
            .ToArray();

        return false;
    }

    public static bool TryParseUuid(string? value, out string normalized)
    {
        if (Guid.TryParse(value?.Trim(), out Guid guid))
        {
            normalized = guid.ToString();
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    /// <summary>
    /// Devolve uma falha (Conflict) se a reserva estiver cancelada ou encerrada;
    /// devolve null se estiver aberta para alterações.
    /// </summary>
    public static Result<T>? EnsureReservationOpen<T>(
        ReservationModel reservation,
        DateTimeOffset now)
    {
        if (reservation.Status == ReservationStatusEnum.Cancelled)
        {
            return Result<T>.Conflict("Reservation is cancelled.");
        }

        if (reservation.EndsAt <= now)
        {
            return Result<T>.Conflict("Reservation has already ended.");
        }

        return null;
    }

    /// <summary>
    /// Devolve Conflict se organizador (1) + participantes atuais + adicionais
    /// ultrapassarem a capacidade da sala; devolve null se couber.
    /// </summary>
    public static Result<T>? EnsureCapacity<T>(
        RoomModel room,
        int currentAttendees,
        int additional = 1)
    {
        if (1 + currentAttendees + additional > room.Capacity)
        {
            return Result<T>.Conflict("Room capacity exceeded.");
        }

        return null;
    }
}