using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class FindReservationByIdUseCase(IUnitOfWork unitOfWork)
    : IFindReservationByIdUseCase
{
    public async Task<Result<ReservationModel>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
            return Result<ReservationModel>.ValidationFailure(["Reservation ID is required."]);

        ReservationModel? reservation = await unitOfWork.ReservationRepository.GetByIdAsync(id, ct);

        if (reservation is null)
            return Result<ReservationModel>.NotFound("Reservation not found.");

        return Result<ReservationModel>.Success(reservation);
    }
}