using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Room.Services.Providers;

public sealed class ExistsRoomByIdUseCase(IUnitOfWork uow)
    : IExistsRoomByIdUseCase
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

        if (!Guid.TryParse(id, out var roomId))
        {
            return Result<bool>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var exists = await uow.RoomRepository.ExistsAsync(
            roomId.ToString(),
            ct);

        return Result<bool>.Success(exists);
    }
}