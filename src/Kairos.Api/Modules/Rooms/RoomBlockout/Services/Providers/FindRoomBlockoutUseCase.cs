using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;

public sealed class FindRoomBlockoutUseCase(IUnitOfWork uow) : IFindRoomBlockoutUseCase
{
    public async Task<Result<RoomBlockoutModel>> ExecuteAsync(
        string id,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Room blockout ID is required."]);
        }

        if (!Guid.TryParse(id, out var blockoutId))
        {
            return Result<RoomBlockoutModel>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        var blockout = await uow.RoomBlockoutRepository
            .GetByIdAsync(blockoutId.ToString(), ct);

        if (blockout is null)
        {
            return Result<RoomBlockoutModel>.NotFound(
                "Room blockout doesn't exist.");
        }

        return Result<RoomBlockoutModel>.Success(blockout);
    }
}