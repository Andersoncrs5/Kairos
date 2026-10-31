using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;

namespace Kairos.Api.Modules.Reservations.Reservation.Services.Providers;

public sealed class ExistsConflictingByRoomUseCase(
    IUnitOfWork unitOfWork)
    : IExistsConflictingByRoomUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        ReservationRoomConflictCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (command is null)
        {
            return Result<bool>.ValidationFailure(
                ["Reservation room conflict command is required."]);
        }

        if (string.IsNullOrWhiteSpace(command.RoomId))
        {
            return Result<bool>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (command.StartsAt >= command.EndsAt)
        {
            return Result<bool>.ValidationFailure(
                ["Reservation start date must be earlier than end date."]);
        }

        bool exists = await unitOfWork.ReservationRepository
            .ExistsConflictingByRoomAsync(
                command.RoomId.Trim(),
                command.StartsAt,
                command.EndsAt,
                ct);

        return Result<bool>.Success(exists);
    }
}