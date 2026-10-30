using Kairos.Api.Common.Result;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;

public interface IFindRoomBlockoutUseCase
{
    Task<Result<RoomBlockoutModel>> ExecuteAsync(
        string id,
        CancellationToken ct = default);
}