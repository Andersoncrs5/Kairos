using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;

public sealed class DeleteRoomBlockoutByIdCase(IUnitOfWork uow)
    : IDeleteRoomBlockoutByIdCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<bool>.ValidationFailure(
                ["Room blockout ID is required."]);
        }

        if (!Guid.TryParse(id, out _))
        {
            return Result<bool>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var blockout = await uow.RoomBlockoutRepository
            .GetByIdAsync(id, ct);

        if (blockout is null)
        {
            return Result<bool>.NotFound(
                "Room blockout doesn't exist.");
        }

        uow.RoomBlockoutRepository.Delete(blockout);

        return Result<bool>.Success(true);
    }
}