using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class GetConflictingByRoomUseCase(IUnitOfWork unitOfWork)
    : IGetConflictingByRoomUseCase
{
    public async Task<Result<IReadOnlyList<ReservationModel>>> ExecuteAsync(string roomId, DateTimeOffset startsAt, DateTimeOffset endsAt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(roomId))
            return Result<IReadOnlyList<ReservationModel>>.ValidationFailure(["Room ID is required."]);

        if (startsAt >= endsAt)
            return Result<IReadOnlyList<ReservationModel>>.ValidationFailure(["Reservation start date must be earlier than end date."]);

        IReadOnlyList<ReservationModel> reservations =
            await unitOfWork.ReservationRepository.GetConflictingByRoomAsync(
                roomId.Trim(),
                startsAt,
                endsAt,
                ct);

        return Result<IReadOnlyList<ReservationModel>>.Success(reservations);
    }
}