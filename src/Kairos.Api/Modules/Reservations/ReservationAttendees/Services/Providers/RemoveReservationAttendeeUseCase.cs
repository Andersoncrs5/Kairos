using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

public sealed class RemoveReservationAttendeeUseCase(IUnitOfWork uow)
    : IRemoveReservationAttendeeUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        RemoveReservationAttendeeCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<bool>.ValidationFailure(
                ["Remove attendee command is required."]);
        }

        if (!ReservationAttendeeRules.TryValidate(
                command,
                "Invalid remove attendee command.",
                out string[] errors))
        {
            return Result<bool>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifiers
        // ---------------------------------------------------------------

        if (!ReservationAttendeeRules.TryParseUuid(
                command.ReservationId,
                out string reservationId))
        {
            return Result<bool>.ValidationFailure(
                ["Reservation ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.AttendeeId,
                out string attendeeId))
        {
            return Result<bool>.ValidationFailure(
                ["Attendee ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<bool>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find attendee (must belong to the reservation in the route)
        // ---------------------------------------------------------------

        ReservationAttendeeModel? attendee =
            await uow.ReservationAttendeeRepository
                .GetByIdAndReservationAsync(attendeeId, reservationId, ct);

        if (attendee is null)
        {
            return Result<bool>.NotFound("Attendee not found.");
        }

        // ---------------------------------------------------------------
        // 4. Find reservation
        //    No lock needed: removing an attendee only frees a seat.
        // ---------------------------------------------------------------

        ReservationModel? reservation =
            await uow.ReservationRepository.GetByIdAsync(reservationId, ct);

        if (reservation is null)
        {
            return Result<bool>.NotFound("Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 5. Authorization: the organizer removes anyone,
        //    an attendee can only remove themselves (leave the meeting)
        // ---------------------------------------------------------------

        bool isOrganizer = reservation.UserId == requesterUserId;
        bool isSelf = attendee.UserId == requesterUserId;

        if (!isOrganizer && !isSelf)
        {
            return Result<bool>.Forbidden(
                "Only the reservation organizer or the attendee can remove this attendee.");
        }

        // ---------------------------------------------------------------
        // 6. Validate reservation status (keeps the history of
        //    cancelled and ended reservations intact)
        // ---------------------------------------------------------------

        Result<bool>? closed =
            ReservationAttendeeRules.EnsureReservationOpen<bool>(
                reservation,
                DateTimeOffset.UtcNow);

        if (closed is not null)
        {
            return closed;
        }

        // ---------------------------------------------------------------
        // 7. Remove attendee (SaveChanges is done by the transaction)
        // ---------------------------------------------------------------

        uow.ReservationAttendeeRepository.Delete(attendee);

        return Result<bool>.Success(true);
    }
}