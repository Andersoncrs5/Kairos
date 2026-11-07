using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

public sealed class UpdateReservationAttendeeUseCase(IUnitOfWork uow)
    : IUpdateReservationAttendeeUseCase
{
    public async Task<Result<ReservationAttendeeModel>> ExecuteAsync(
        UpdateReservationAttendeeCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(["Attendee update command is required."]);
        }

        if (!ReservationAttendeeRules.TryValidate(command, "Invalid attendee update command.", out string[] errors))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        UpdateReservationAttendeeDto dto = command.Dto;

        if (!ReservationAttendeeRules.TryValidate(
                dto,
                "Invalid attendee update data.",
                out errors))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 3. Validate identifiers
        // ---------------------------------------------------------------

        if (!ReservationAttendeeRules.TryParseUuid(
                command.ReservationId,
                out string reservationId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Reservation ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.AttendeeId,
                out string attendeeId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Attendee ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Find reservation
        // ---------------------------------------------------------------

        ReservationModel? reservation =
            await uow.ReservationRepository.GetByIdAsync(reservationId, ct);

        if (reservation is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 5. Only the organizer can change an attendee's role
        //    (checked before loading the attendee, so non-organizers
        //    cannot probe which attendee IDs exist)
        // ---------------------------------------------------------------

        if (reservation.UserId != requesterUserId)
        {
            return Result<ReservationAttendeeModel>.Forbidden(
                "Only the reservation organizer can update attendees.");
        }

        // ---------------------------------------------------------------
        // 6. Find attendee (must belong to the reservation in the route)
        // ---------------------------------------------------------------

        ReservationAttendeeModel? attendee =
            await uow.ReservationAttendeeRepository
                .GetByIdAndReservationAsync(attendeeId, reservationId, ct);

        if (attendee is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Attendee not found.");
        }

        // ---------------------------------------------------------------
        // 7. Validate reservation status
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

        // ---------------------------------------------------------------
        // 8. Nothing to change (idempotent)
        // ---------------------------------------------------------------

        if (attendee.Role == dto.Role)
        {
            return Result<ReservationAttendeeModel>.Success(attendee);
        }

        // ---------------------------------------------------------------
        // 9. Apply change
        // ---------------------------------------------------------------

        attendee.Role = dto.Role;

        // ---------------------------------------------------------------
        // 10. Update audit fields
        // ---------------------------------------------------------------

        attendee.UpdatedAt = now;

        // The attendee was loaded with AsNoTracking, so it must be attached
        uow.ReservationAttendeeRepository.Update(attendee);

        return Result<ReservationAttendeeModel>.Success(attendee);
    }
}