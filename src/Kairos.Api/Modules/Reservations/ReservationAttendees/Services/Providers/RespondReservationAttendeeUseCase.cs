using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

public sealed class RespondReservationAttendeeUseCase(IUnitOfWork uow)
    : IRespondReservationAttendeeUseCase
{
    public async Task<Result<ReservationAttendeeModel>> ExecuteAsync(
        RespondReservationAttendeeCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Response command is required."]);
        }

        if (!ReservationAttendeeRules.TryValidate(
                command,
                "Invalid response command.",
                out string[] errors))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO (also rejects Status = Pending)
        // ---------------------------------------------------------------

        RespondReservationAttendeeDto dto = command.Dto;

        if (!ReservationAttendeeRules.TryValidate(
                dto,
                "Invalid response data.",
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
                command.UserId,
                out string userId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Attendee user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Find the invitation (the user can only answer their own)
        // ---------------------------------------------------------------

        ReservationAttendeeModel? attendee =
            await uow.ReservationAttendeeRepository
                .GetByReservationAndUserAsync(reservationId, userId, ct);

        if (attendee is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Invitation not found.");
        }

        // ---------------------------------------------------------------
        // 5. Find reservation
        //    Going from Declined to Accepted/Tentative takes a seat again,
        //    so in that case the reservation row is locked to serialize
        //    the capacity check (requires an active transaction).
        // ---------------------------------------------------------------

        bool reoccupiesSeat =
            attendee.Status == AttendeeStatusEnum.Declined &&
            dto.Status != AttendeeStatusEnum.Declined;

        ReservationModel? reservation = reoccupiesSeat
            ? await uow.ReservationRepository.FindByIdWithForUpdateAsync(
                reservationId,
                cancellationToken: ct)
            : await uow.ReservationRepository.GetByIdAsync(
                reservationId,
                ct);

        if (reservation is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 6. Validate reservation status
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
        // 7. Normalize note and handle repeated answers (idempotent)
        // ---------------------------------------------------------------

        string? responseNote = string.IsNullOrWhiteSpace(dto.ResponseNote)
            ? null
            : dto.ResponseNote.Trim();

        if (attendee.Status == dto.Status &&
            attendee.ResponseNote == responseNote)
        {
            return Result<ReservationAttendeeModel>.Success(attendee);
        }

        // ---------------------------------------------------------------
        // 8. Check room capacity when taking a seat again
        // ---------------------------------------------------------------

        if (reoccupiesSeat)
        {
            RoomModel? room =
                await uow.RoomRepository.GetByIdAsync(reservation.RoomId, ct);

            if (room is null)
            {
                return Result<ReservationAttendeeModel>.NotFound(
                    "Room doesn't exist.");
            }

            int occupying =
                await uow.ReservationAttendeeRepository
                    .CountOccupyingByReservationAsync(reservationId, ct);

            Result<ReservationAttendeeModel>? noCapacity =
                ReservationAttendeeRules.EnsureCapacity<ReservationAttendeeModel>(
                    room,
                    occupying);

            if (noCapacity is not null)
            {
                return noCapacity;
            }
        }

        // ---------------------------------------------------------------
        // 9. Apply response
        // ---------------------------------------------------------------

        attendee.Status = dto.Status;
        attendee.ResponseNote = responseNote;
        attendee.RespondedAt = now;

        // ---------------------------------------------------------------
        // 10. Update audit fields
        // ---------------------------------------------------------------

        attendee.UpdatedAt = now;

        // The attendee was loaded with AsNoTracking, so it must be attached
        uow.ReservationAttendeeRepository.Update(attendee);

        return Result<ReservationAttendeeModel>.Success(attendee);
    }
}