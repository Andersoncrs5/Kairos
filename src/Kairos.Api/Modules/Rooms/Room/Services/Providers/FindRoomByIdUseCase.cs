using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Room.Services.Providers;

public sealed class FindRoomByIdUseCase(IUnitOfWork uow)
    : IFindRoomByIdUseCase
{
    public async Task<Result<RoomModel>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<RoomModel>.ValidationFailure(
                ["Room ID is required."]);
        }

        if (!Guid.TryParse(id, out var roomId))
        {
            return Result<RoomModel>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var room = await uow.RoomRepository.GetByIdAsync(roomId.ToString(), ct);

        if (room is null)
        {
            return Result<RoomModel>.NotFound(
                "Room doesn't exist.");
        }

        return Result<RoomModel>.Success(room);
    }
}