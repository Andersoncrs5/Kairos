using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class DeleteReservationByIdUseCase(IUnitOfWork unitOfWork)
    : IDeleteReservationByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Reservation ID is required."]);
        }

        int deletedCount = await unitOfWork.ReservationRepository.DeleteByIdAndCountAsync(id.Trim(), ct);

        return Result<bool>.Success(deletedCount > 0);
    }
}