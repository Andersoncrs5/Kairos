using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.Mapper;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class UpdateReservationUseCase(IUnitOfWork uow): IUpdateReservationUseCase
{
    public async Task<Result<ReservationModel>> ExecuteAsync(
        UpdateReservationCommand command,
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

        UpdateReservationDto dto = command.Dto;

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
        // 3. Validate reservation ID
        // ---------------------------------------------------------------

        if (!Guid.TryParse(command.ReservationId, out Guid reservationGuid))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation ID must be a valid UUID."]);
        }

        string reservationId = reservationGuid.ToString();

        // ---------------------------------------------------------------
        // 4. Get current reservation
        // ---------------------------------------------------------------

        ReservationModel? reservation =
            await uow.ReservationRepository.GetByIdAsync(
                reservationId,
                ct);

        if (reservation is null)
        {
            return Result<ReservationModel>.NotFound(
                "Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 5. Validate and resolve room
        // ---------------------------------------------------------------

        string roomId = reservation.RoomId;
        bool roomChanged = false;
        var updatedRoom = reservation.Room;

        if (dto.RoomId is not null)
        {
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

            roomId = roomGuid.ToString();

            bool currentRoomIsValid =
                Guid.TryParse(reservation.RoomId, out Guid currentRoomGuid);

            roomChanged = !currentRoomIsValid ||
                          currentRoomGuid != roomGuid;

            if (roomChanged)
            {
                var room = await uow.RoomRepository.GetByIdAsync(
                    roomId,
                    ct);

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

                updatedRoom = room;
            }
        }

        // ---------------------------------------------------------------
        // 6. Validate and resolve category
        // ---------------------------------------------------------------

        string categoryId = reservation.CategoryId;
        bool categoryChanged = false;
        var updatedCategory = reservation.Category;

        if (dto.CategoryId is not null)
        {
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

            categoryId = categoryGuid.ToString();

            bool currentCategoryIsValid =
                Guid.TryParse(reservation.CategoryId, out Guid currentCategoryGuid);

            categoryChanged = !currentCategoryIsValid ||
                              currentCategoryGuid != categoryGuid;

            if (categoryChanged)
            {
                var category = await uow.CategoryRepository.GetByIdAsync(
                    categoryId,
                    ct);

                if (category is null)
                {
                    return Result<ReservationModel>.NotFound(
                        "Category not found.");
                }

                if (!category.IsActive)
                {
                    return Result<ReservationModel>.BadRequest(
                        "Category is not active.");
                }

                updatedCategory = category;
            }
        }

        // ---------------------------------------------------------------
        // 7. Validate reservation period
        // ---------------------------------------------------------------

        if ((dto.StartsAt.HasValue && dto.StartsAt.Value == default(DateTimeOffset)) ||
            (dto.EndsAt.HasValue && dto.EndsAt.Value == default(DateTimeOffset)))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation dates cannot be default values."]);
        }

        // Use the provided date when present; otherwise keep the current one.
        DateTimeOffset startsAt = dto.StartsAt ?? reservation.StartsAt;
        DateTimeOffset endsAt = dto.EndsAt ?? reservation.EndsAt;

        if (startsAt >= endsAt)
            return Result<ReservationModel>.ValidationFailure(["Reservation start date must be earlier than end date."]);

        bool periodChanged = startsAt != reservation.StartsAt || endsAt != reservation.EndsAt;

        // ---------------------------------------------------------------
        // 8. Check reservation conflicts
        // ---------------------------------------------------------------

        // Only check conflicts when the room or reservation period changes.
        if (roomChanged || periodChanged)
        {
            bool hasReservationConflict = 
                await uow.ReservationRepository.ExistsConflictingByRoomAsync(
                    roomId,
                    startsAt,
                    endsAt,
                    reservationId,
                    ct);

            if (hasReservationConflict)
            {
                return Result<ReservationModel>.Conflict("Room already has a reservation during this period.");
            }

            // -----------------------------------------------------------
            // 9. Check room blockout conflicts
            // -----------------------------------------------------------

            var overlappingBlockouts =
                await uow.RoomBlockoutRepository.GetOverlappingAsync(
                    roomId,
                    startsAt,
                    endsAt,
                    ct);

            bool hasActiveBlockout = overlappingBlockouts.Any(blockout => blockout.Status != RoomBlockoutStatus.Cancelled);

            if (hasActiveBlockout)
            {
                return Result<ReservationModel>.Conflict("Room is blocked during this period.");
            }
        }

        // ---------------------------------------------------------------
        // 10. Validate and normalize optional values
        // ---------------------------------------------------------------

        if (dto.Title is not null && string.IsNullOrWhiteSpace(dto.Title))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation title cannot be empty."]);
        }

        string? title = dto.Title?.Trim();

        string? description = dto.Description is null
            ? null
            : string.IsNullOrWhiteSpace(dto.Description)
                ? null
                : dto.Description.Trim();

        // ---------------------------------------------------------------
        // 11. Apply updates
        // ---------------------------------------------------------------

        ReservationMapper.ApplyUpdate(dto, reservation);

        reservation.RoomId = roomId;
        reservation.CategoryId = categoryId;

        reservation.StartsAt = startsAt;
        reservation.EndsAt = endsAt;

        if (roomChanged)
            reservation.Room = updatedRoom;

        if (categoryChanged)
            reservation.Category = updatedCategory;

        if (title is not null)
            reservation.Title = title;

        if (dto.Description is not null)
            reservation.Description = description;

        // ---------------------------------------------------------------
        // 12. Update audit fields
        // ---------------------------------------------------------------

        reservation.UpdatedAt = DateTimeOffset.UtcNow;
        reservation.Version++;

        return Result<ReservationModel>.Success(reservation);
    }
}