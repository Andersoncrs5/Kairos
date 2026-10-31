using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class FindReservationsUseCase(IUnitOfWork uow)
    : IFindReservationsUseCase
{
    public async Task<Result<Page<ReservationModel>>> ExecuteAsync(
        FilterReservationDto dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate DTO
        // ---------------------------------------------------------------

        if (dto is null)
        {
            return Result<Page<ReservationModel>>.ValidationFailure(
                ["Reservation filter is required."]);
        }

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<Page<ReservationModel>>.ValidationFailure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid reservation filter.")
                    .Distinct()
                    .ToArray());
        }

        // ---------------------------------------------------------------
        // 2. Find reservations
        // ---------------------------------------------------------------

        Page<ReservationModel> reservations =
            await uow.ReservationRepository.Find(dto, ct);

        // ---------------------------------------------------------------
        // 3. Return result
        // ---------------------------------------------------------------

        return Result<Page<ReservationModel>>.Success(reservations);
    }
}