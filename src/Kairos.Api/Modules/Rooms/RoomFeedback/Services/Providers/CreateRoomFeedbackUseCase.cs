using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.ReservationAttendees.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFeedback.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFeedback.Mapper;
using Kairos.Api.Modules.Rooms.RoomFeedback.Models;
using Kairos.Api.Modules.Rooms.RoomFeedback.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomFeedback.Services.Providers;

public sealed class CreateRoomFeedbackUseCase(IUnitOfWork uow)
    : ICreateRoomFeedbackUseCase
{
    public async Task<Result<RoomFeedbackModel>> ExecuteAsync(
        CreateRoomFeedbackCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<RoomFeedbackModel>.ValidationFailure(
                ["Room feedback command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid room feedback command.",
                out string[] errors))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        CreateRoomFeedbackDto dto = command.Dto;

        if (!UseCaseValidation.TryValidate(
                dto,
                "Invalid room feedback data.",
                out errors))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 3. Validate identifiers
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.ReservationId,
                out string reservationId))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(
                ["Reservation ID must be a valid UUID."]);
        }

        if (!UseCaseValidation.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<RoomFeedbackModel>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Find reservation
        // ---------------------------------------------------------------

        ReservationModel? reservation =
            await uow.ReservationRepository.GetByIdAsync(reservationId, ct);

        if (reservation is null)
        {
            return Result<RoomFeedbackModel>.NotFound(
                "Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 5. Only the organizer or an attendee who accepted can review.
        //    Checked before the reservation status so that outsiders
        //    cannot learn anything about reservations that aren't theirs.
        // ---------------------------------------------------------------

        if (reservation.UserId != requesterUserId)
        {
            ReservationAttendeeModel? attendee =
                await uow.ReservationAttendeeRepository
                    .GetByReservationAndUserAsync(
                        reservationId,
                        requesterUserId,
                        ct);

            if (attendee is null ||
                attendee.Status != AttendeeStatusEnum.Accepted)
            {
                return Result<RoomFeedbackModel>.Forbidden(
                    "Only the organizer or attendees who accepted the invitation can review this reservation.");
            }
        }

        // ---------------------------------------------------------------
        // 6. The reservation must have taken place
        // ---------------------------------------------------------------

        if (reservation.Status == ReservationStatusEnum.Cancelled)
        {
            return Result<RoomFeedbackModel>.Conflict(
                "Cancelled reservations cannot be reviewed.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (reservation.EndsAt > now)
        {
            return Result<RoomFeedbackModel>.Conflict(
                "Reservation has not ended yet.");
        }

        // ---------------------------------------------------------------
        // 7. One review per person per reservation
        // ---------------------------------------------------------------

        bool alreadyReviewed =
            await uow.RoomFeedbackRepository
                .ExistsByReservationAndUserAsync(
                    reservationId,
                    requesterUserId,
                    ct);

        if (alreadyReviewed)
        {
            return Result<RoomFeedbackModel>.Conflict(
                "You have already reviewed this reservation.");
        }

        // ---------------------------------------------------------------
        // 8. Create feedback (the room always comes from the reservation)
        // ---------------------------------------------------------------

        RoomFeedbackModel feedback = RoomFeedbackMapper.ToModel(
            dto,
            Guid.NewGuid().ToString(),
            reservationId,
            reservation.RoomId,
            requesterUserId,
            now);

        // ---------------------------------------------------------------
        // 9. Persist feedback (SaveChanges is done by the transaction)
        // ---------------------------------------------------------------

        await uow.RoomFeedbackRepository.AddAsync(feedback, ct);

        return Result<RoomFeedbackModel>.Created(feedback);
    }
}