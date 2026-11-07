using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Identity.Users.Models;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Commands;
using Kairos.Api.Modules.Reservations.ReservationAttendees.DTOs.Request;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Interfaces;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Rules;
using Kairos.Api.Modules.Rooms.Room.Models;

namespace Kairos.Api.Modules.Reservations.ReservationAttendees.Services.Providers;

public sealed class AddReservationAttendeeUseCase(IUnitOfWork uow): IAddReservationAttendeeUseCase
{
    public async Task<Result<ReservationAttendeeModel>> ExecuteAsync(AddReservationAttendeeCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
            return Result<ReservationAttendeeModel>.ValidationFailure(["Attendee command is required."]);

        if (!ReservationAttendeeRules.TryValidate(command, "Invalid attendee command.", out string[] errors))
            return Result<ReservationAttendeeModel>.ValidationFailure(errors);

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        AddReservationAttendeeDto dto = command.Dto;

        if (!ReservationAttendeeRules.TryValidate(
                dto,
                "Invalid attendee data.",
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
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        if (!ReservationAttendeeRules.TryParseUuid(
                dto.UserId,
                out string attendeeUserId))
        {
            return Result<ReservationAttendeeModel>.ValidationFailure(
                ["Attendee user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Find reservation (row lock: serializes invites of the same
        //    reservation, requires an active transaction)
        // ---------------------------------------------------------------

        ReservationModel? reservation =
            await uow.ReservationRepository.FindByIdWithForUpdateAsync(
                reservationId,
                cancellationToken: ct);

        if (reservation is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 5. Only the organizer can add attendees
        // ---------------------------------------------------------------

        if (reservation.UserId != requesterUserId)
        {
            return Result<ReservationAttendeeModel>.Forbidden(
                "Only the reservation organizer can add attendees.");
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
        // 7. Validate attendee user
        // ---------------------------------------------------------------

        if (attendeeUserId == reservation.UserId)
        {
            return Result<ReservationAttendeeModel>.BadRequest(
                "The organizer is already part of the reservation.");
        }

        UserModel? attendeeUser =
            await uow.UserRepository.GetByIdAsync(attendeeUserId);

        if (attendeeUser is null)
        {
            return Result<ReservationAttendeeModel>.NotFound(
                "Attendee user not found.");
        }

        if (!attendeeUser.IsActive)
        {
            return Result<ReservationAttendeeModel>.BadRequest(
                "Attendee user is not active.");
        }

        // ---------------------------------------------------------------
        // 8. Check duplicate invitation
        // ---------------------------------------------------------------

        bool alreadyAttendee =
            await uow.ReservationAttendeeRepository
                .ExistsByReservationAndUserAsync(
                    reservationId,
                    attendeeUserId,
                    ct);

        if (alreadyAttendee)
        {
            return Result<ReservationAttendeeModel>.Conflict(
                "User is already an attendee of this reservation.");
        }

        // ---------------------------------------------------------------
        // 9. Check room capacity
        // ---------------------------------------------------------------

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

        // ---------------------------------------------------------------
        // 10. Create attendee
        // ---------------------------------------------------------------

        ReservationAttendeeModel attendee = new()
        {
            Id = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            UserId = attendeeUserId,
            User = attendeeUser,
            Role = dto.Role,
            Status = AttendeeStatusEnum.Pending,
            InvitedBy = requesterUserId,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = null
        };

        // ---------------------------------------------------------------
        // 11. Persist attendee (SaveChanges is done by the transaction)
        // ---------------------------------------------------------------

        await uow.ReservationAttendeeRepository.AddAsync(attendee, ct);

        return Result<ReservationAttendeeModel>.Created(attendee);
    }
}