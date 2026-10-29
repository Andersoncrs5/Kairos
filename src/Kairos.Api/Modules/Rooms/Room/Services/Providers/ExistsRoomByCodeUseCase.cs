using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.Room.Services.Providers;

public sealed class ExistsRoomByCodeUseCase(IUnitOfWork uow)
    : IExistsRoomByCodeUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string code,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<bool>.ValidationFailure(
                ["Room code is required."]);
        }

        code = code.Trim();

        if (code.Length > 30)
        {
            return Result<bool>.ValidationFailure(
                ["Room code cannot exceed 30 characters."]);
        }

        var exists = await uow.RoomRepository.ExistsByCodeAsync(
            code,
            ct);

        return Result<bool>.Success(exists);
    }
}