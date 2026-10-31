using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Enums;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class CancelReservationUseCase(IUnitOfWork uow)
    : ICancelReservationUseCase
{
    public async Task<Result<ReservationModel>> ExecuteAsync(
        CancelReservationCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Cancellation command is required."]);
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
                        result.ErrorMessage ?? "Invalid cancellation command.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 2. Validate DTO
        // ---------------------------------------------------------------

        var dto = command.Dto;

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
                        result.ErrorMessage ?? "Invalid cancellation data.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 3. Validate identifiers
        // ---------------------------------------------------------------

        if (!Guid.TryParse(command.ReservationId, out Guid reservationGuid))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Reservation ID must be a valid UUID."]);
        }

        string reservationId = reservationGuid.ToString();

        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Cancellation user ID is required."]);
        }

        if (!Guid.TryParse(command.UserId.Trim(), out Guid userGuid))
        {
            return Result<ReservationModel>.ValidationFailure(
                ["Cancellation user ID must be a valid UUID."]);
        }

        string userId = userGuid.ToString();

        // ---------------------------------------------------------------
        // 4. Find reservation
        // ---------------------------------------------------------------

        ReservationModel? reservation = await uow.ReservationRepository.GetByIdAsync(reservationId, ct);

        if (reservation is null)
        {
            return Result<ReservationModel>.NotFound("Reservation not found.");
        }

        // ---------------------------------------------------------------
        // 5. Validate reservation status
        // ---------------------------------------------------------------

        if (reservation.Status == ReservationStatusEnum.Cancelled)
        {
            return Result<ReservationModel>.Conflict(
                "Reservation is already cancelled.");
        }

        // ---------------------------------------------------------------
        // 6. Normalize cancellation reason
        // ---------------------------------------------------------------

        string? cancellationReason =
            string.IsNullOrWhiteSpace(dto.CancellationReason)
                ? null
                : dto.CancellationReason.Trim();

        // ---------------------------------------------------------------
        // 7. Cancel reservation
        // ---------------------------------------------------------------

        DateTimeOffset now = DateTimeOffset.UtcNow;

        reservation.Status = ReservationStatusEnum.Cancelled;
        reservation.CancelledAt = now;
        reservation.CancelledBy = userId;
        reservation.CancellationReason = cancellationReason;

        // ---------------------------------------------------------------
        // 8. Update audit fields
        // ---------------------------------------------------------------

        reservation.UpdatedAt = now;
        reservation.Version++;

        return Result<ReservationModel>.Success(reservation);
    }
}