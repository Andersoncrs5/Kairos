using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;

public sealed class ExistsRoomBlockoutByIdUseCase(IUnitOfWork uow)
    : IExistsRoomBlockoutByIdUseCase
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

        if (!Guid.TryParse(id, out var blockoutId))
        {
            return Result<bool>.ValidationFailure(
                ["Id must be a valid UUID."]);
        }

        RoomBlockoutModel? blockout = await uow.RoomBlockoutRepository.GetByIdAsync(blockoutId.ToString(), ct);

        return Result<bool>.Success(blockout is not null);
    }
}