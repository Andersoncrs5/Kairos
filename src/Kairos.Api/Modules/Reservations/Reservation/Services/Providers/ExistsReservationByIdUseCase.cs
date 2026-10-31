using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class ExistsReservationByIdUseCase(IUnitOfWork unitOfWork)
    : IExistsReservationByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
            return Result<bool>.ValidationFailure(["Reservation ID is required."]);

        bool exists = await unitOfWork.ReservationRepository.ExistsAsync(id.Trim(), ct);

        return Result<bool>.Success(exists);
    }
}