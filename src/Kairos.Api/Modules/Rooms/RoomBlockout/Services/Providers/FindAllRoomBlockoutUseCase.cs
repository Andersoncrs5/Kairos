using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Providers;

public sealed class FindAllRoomBlockoutUseCase(
    IUnitOfWork uow)
    : IFindAllRoomBlockoutUseCase
{
    public async Task<Result<Page<RoomBlockoutModel>>> ExecuteAsync(
        FilterRoomBlockoutDto dto,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (dto is null)
        {
            return Result<Page<RoomBlockoutModel>>.ValidationFailure(
                ["Room blockout filter is required."]);
        }

        Page<RoomBlockoutModel> page =
            await uow.RoomBlockoutRepository.Find(dto, ct);

        return Result<Page<RoomBlockoutModel>>.Success(page);
    }
}