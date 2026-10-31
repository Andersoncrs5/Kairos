using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class GetConflictingByRoomUseCase(
    IUnitOfWork unitOfWork)
    : IGetConflictingByRoomUseCase
{
    public async Task<Result<IReadOnlyList<ReservationModel>>> ExecuteAsync(
        ReservationRoomConflictCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<IReadOnlyList<ReservationModel>>.ValidationFailure(
                ["Reservation room conflict command is required."]);
        }

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            return Result<IReadOnlyList<ReservationModel>>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (command.StartsAt >= command.EndsAt)
        {
            return Result<IReadOnlyList<ReservationModel>>.ValidationFailure(
                ["Reservation start date must be earlier than end date."]);
        }

        IReadOnlyList<ReservationModel> reservations =
            await unitOfWork.ReservationRepository.GetConflictingByRoomAsync(
                command.RoomId.Trim(),
                command.StartsAt,
                command.EndsAt,
                ct);

        return Result<IReadOnlyList<ReservationModel>>.Success(
            reservations);
    }
}