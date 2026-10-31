using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class ExistsConflictingByRoomUseCase(IUnitOfWork unitOfWork)
    : IExistsConflictingByRoomUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string roomId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId))
        {
            return Result<bool>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (startsAt >= endsAt)
        {
            return Result<bool>.ValidationFailure(
                ["Reservation start date must be earlier than end date."]);
        }

        bool exists = await unitOfWork.ReservationRepository
            .ExistsConflictingByRoomAsync(
                roomId.Trim(),
                startsAt,
                endsAt,
                ct);

        return Result<bool>.Success(exists);
    }
}