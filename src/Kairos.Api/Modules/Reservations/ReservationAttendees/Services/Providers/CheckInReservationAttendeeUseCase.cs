using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

public sealed class CheckInReservationAttendeeUseCase(IUnitOfWork uow): ICheckInReservationAttendeeUseCase
{
    public async Task<Result<ReservationAttendeeModel>> ExecuteAsync(CheckInReservationAttendeeCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Check-in command is required."]);
        }

        if (!ReservationAttendeeRules.TryValidate(
                command,
                "Invalid check-in command.",
                out string[] errors))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifiers
        // ---------------------------------------------------------------

        if (!ReservationAttendeeRules.TryParseUuid(
                command.ReservationId,
                out string reservationId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Reservation ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.AttendeeUserId,
                out string attendeeUserId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Attendee user ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find reservation
        // ---------------------------------------------------------------

        ReservationModel? reservation =
            await uow.ReservationRepository.GetByIdAsync(reservationId, ct);

        if (reservation is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 4. Authorization: the attendee or the organizer
        // ---------------------------------------------------------------

        if (requesterUserId != attendeeUserId &&
            requesterUserId != reservation.UserId)
        {
            return Result<ReservationAttendeeModel>.Forbidden(
                "Only the attendee or the reservation organizer can check in.");
        }

        // ---------------------------------------------------------------
        // 5. Find attendee
        // ---------------------------------------------------------------

        ReservationAttendeeModel? attendee =
            await uow.ReservationAttendeeRepository
                .GetByReservationAndUserAsync(
                    reservationId,
                    attendeeUserId,
                    ct);

        if (attendee is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Attendee not found.");
        }

        // ---------------------------------------------------------------
        // 6. Validate reservation status and check-in window
        // ---------------------------------------------------------------

        DateTimeOffset now = DateTimeOffset.UtcNow;

        Result<ReservationAttendeeModel>? closed =
            ReservationAttendeeRules.EnsureReservationOpen<ReservationAttendeeModel>(
                reservation,
                now);

        if (closed is not null)
        {
            return closed;
        }

        if (now < reservation.StartsAt)
        {
            return Result<ReservationAttendeeModel>.Conflict(
                "Check-in is only available after the reservation starts.");
        }

        // ---------------------------------------------------------------
        // 7. Only attendees who accepted can check in
        // ---------------------------------------------------------------

        if (attendee.Status != AttendeeStatusEnum.Accepted)
        {
            return Result<ReservationAttendeeModel>.Conflict(
                "Only attendees who accepted the invitation can check in.");
        }

        // ---------------------------------------------------------------
        // 8. Already checked in (idempotent)
        // ---------------------------------------------------------------

        if (attendee.CheckedInAt.HasValue)
        {
            return Result<ReservationAttendeeModel>.Success(attendee);
        }

        // ---------------------------------------------------------------
        // 9. Apply check-in and update audit fields
        // ---------------------------------------------------------------

        attendee.CheckedInAt = now;
        attendee.UpdatedAt = now;
        attendee.Version++;

        // The attendee was loaded with AsNoTracking, so it must be attached
        uow.ReservationAttendeeRepository.Update(attendee);

        return Result<ReservationAttendeeModel>.Success(attendee);
    }
}