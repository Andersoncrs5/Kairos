
using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.Mapper;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class CreateReservationUseCase(IUnitOfWork uow)
    : ICreateReservationUseCase
{
    public async Task<Result<ReservationModel>> ExecuteAsync(
        CreateReservationCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation command is required."]);
        }

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<ReservationModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid reservation command.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        CreateReservationDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<ReservationModel>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid reservation data.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 3. Validate and normalize identifiers
        // ---------------------------------------------------------------

        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation user ID is required."]);
        }

        if (!Guid.TryParse(command.UserId.Trim(), out Guid userGuid))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation user ID must be a valid UUID."]);
        }

        string userId = userGuid.ToString();

        if (string.IsNullOrWhiteSpace(dto.RoomId))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (!Guid.TryParse(dto.RoomId.Trim(), out Guid roomGuid))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Room ID must be a valid UUID."]);
        }

        string roomId = roomGuid.ToString();

        if (string.IsNullOrWhiteSpace(dto.CategoryId))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Category ID is required."]);
        }

        if (!Guid.TryParse(dto.CategoryId.Trim(), out Guid categoryGuid))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Category ID must be a valid UUID."]);
        }

        string categoryId = categoryGuid.ToString();

        CategoryModel? categoryModel =
            await uow.CategoryRepository.GetByIdAsync(categoryId, ct);

        if (categoryModel is null)
        {
            return Result<ReservationModel>.NotFound("Category not found.");
        }

        if (!categoryModel.IsActive)
        {
            return Result<ReservationModel>.BadRequest(
                "Category is not active.");
        }

        // ---------------------------------------------------------------
        // 4. Validate reservation period
        // ---------------------------------------------------------------

        if (dto.StartsAt == default || dto.EndsAt == default)
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation start and end dates are required."]);
        }

        if (dto.StartsAt >= dto.EndsAt)
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation start date must be earlier than end date."]);
        }

        // ---------------------------------------------------------------
        // 5. Normalize optional values
        // ---------------------------------------------------------------

        string title = dto.Title.Trim();

        string? description = string.IsNullOrWhiteSpace(dto.Description)
            ? null
            : dto.Description.Trim();

        string? idempotencyKey = string.IsNullOrWhiteSpace(dto.IdempotencyKey)
            ? null
            : dto.IdempotencyKey.Trim();

        // ---------------------------------------------------------------
        // 6. Check idempotency
        // ---------------------------------------------------------------

        if (idempotencyKey is not null)
        {
            ReservationModel? existingReservation =
                await uow.ReservationRepository.GetByIdempotencyKeyAsync(
                    userId,
                    idempotencyKey,
                    ct);

            if (existingReservation is not null)
            {
                return Result<ReservationModel>.Success(existingReservation);
            }
        }

        // ---------------------------------------------------------------
        // 7. Validate room
        // ---------------------------------------------------------------

        var room = await uow.RoomRepository.GetByIdAsync(roomId, ct);

        if (room is null)
        {
            return Result<ReservationModel>.NotFound(
                "Room doesn't exist.");
        }

        if (!room.IsActive)
        {
            return Result<ReservationModel>.BadRequest(
                "Room is not active.");
        }

        if (!room.IsReservable)
        {
            return Result<ReservationModel>.BadRequest(
                "Room is not available for reservations.");
        }

        // ---------------------------------------------------------------
        // 8. Validate room operating hours
        // ---------------------------------------------------------------

        IReadOnlyList<RoomOperatingHourModel> operatingHours =
            await uow.RoomOperatingHourRepository.GetByRoomIdAsync(
                roomId,
                ct);

        bool isRoomOpen = IsWithinOperatingHours(
            operatingHours,
            dto.StartsAt,
            dto.EndsAt);

        if (!isRoomOpen)
        {
            return Result<ReservationModel>.Conflict(
                "Room is closed or outside its operating hours during this period.");
        }

        // ---------------------------------------------------------------
        // 9. Check reservation conflicts
        // ---------------------------------------------------------------

        bool hasReservationConflict =
            await uow.ReservationRepository.ExistsConflictingByRoomAsync(
                roomId,
                dto.StartsAt,
                dto.EndsAt,
                ct);

        if (hasReservationConflict)
        {
            return Result<ReservationModel>.Conflict(
                "Room already has a reservation during this period.");
        }

        // ---------------------------------------------------------------
        // 10. Check room blockout conflicts
        // ---------------------------------------------------------------

        var overlappingBlockouts =
            await uow.RoomBlockoutRepository.GetOverlappingAsync(
                roomId,
                dto.StartsAt,
                dto.EndsAt,
                ct);

        bool hasActiveBlockout = overlappingBlockouts
            .Any(blockout =>
                blockout.Status != RoomBlockoutStatus.Cancelled);

        if (hasActiveBlockout)
        {
            return Result<ReservationModel>.Conflict(
                "Room is blocked during this period.");
        }

        // ---------------------------------------------------------------
        // 11. Create reservation
        // ---------------------------------------------------------------

        DateTimeOffset now = DateTimeOffset.UtcNow;

        ReservationModel reservation = ReservationMapper.ToModel(
            dto,
            Guid.NewGuid().ToString(),
            userId,
            now);

        reservation.RoomId = roomId;
        reservation.Room = room;

        reservation.UserId = userId;

        reservation.CategoryId = categoryId;
        reservation.Category = categoryModel;

        reservation.Title = title;
        reservation.Description = description;
        reservation.IdempotencyKey = idempotencyKey;

        reservation.Status = ReservationStatusEnum.Confirmed;
        reservation.Version = 1;
        reservation.CreatedAt = now;
        reservation.UpdatedAt = null;

        // ---------------------------------------------------------------
        // 12. Persist reservation
        // ---------------------------------------------------------------

        await uow.ReservationRepository.AddAsync(
            reservation,
            ct);

        return Result<ReservationModel>.Success(reservation);
    }

    // ---------------------------------------------------------------
    // Validate reservation against room operating hours
    // ---------------------------------------------------------------

    private static bool IsWithinOperatingHours(
        IReadOnlyList<RoomOperatingHourModel> operatingHours,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt)
    {
        DateTime current = startsAt.DateTime;
        DateTime reservationEnd = endsAt.DateTime;

        while (current < reservationEnd)
        {
            DateTime currentDay = current.Date;
            DateTime nextDay = currentDay.AddDays(1);

            DateTime segmentEnd = reservationEnd < nextDay
                ? reservationEnd
                : nextDay;

            RoomOperatingHourModel[] dayOperatingHours = operatingHours
                .Where(hour =>
                    hour.IsActive &&
                    hour.DayOfWeek == current.DayOfWeek)
                .ToArray();

            // No active configuration means the room is closed.
            if (dayOperatingHours.Length == 0)
            {
                return false;
            }

            // An active closed record blocks the entire day.
            if (dayOperatingHours.Any(hour => hour.IsClosed))
            {
                return false;
            }

            // The entire daily reservation segment must fit
            // inside one continuous operating period.
            bool isInsideOperatingPeriod = dayOperatingHours
                .Where(hour => !hour.IsClosed)
                .Any(hour =>
                {
                    DateTime opensAt = currentDay.Add(
                        hour.OpensAt.ToTimeSpan());

                    DateTime closesAt = currentDay.Add(
                        hour.ClosesAt.ToTimeSpan());

                    return current >= opensAt &&
                           segmentEnd <= closesAt;
                });

            if (!isInsideOperatingPeriod)
            {
                return false;
            }

            current = segmentEnd;
        }

        return true;
    }
}
