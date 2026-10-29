using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Room.Services.Providers;

public sealed class DeleteRoomByIdUseCase(IUnitOfWork uow)
    : IDeleteRoomByIdUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (!Guid.TryParse(id, out _))
        {
            return Result<bool>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var room = await uow.RoomRepository.GetByIdAsync(id, ct);

        if (room is null)
        {
            return Result<bool>.NotFound(
                "Room doesn't exist.");
        }

        uow.RoomRepository.Delete(room);

        return Result<bool>.Success(true);
    }
}