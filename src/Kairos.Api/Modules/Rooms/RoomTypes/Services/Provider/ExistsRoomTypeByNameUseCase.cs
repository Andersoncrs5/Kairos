using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Services.Provider;

public sealed class ExistsRoomTypeByNameUseCase(IUnitOfWork uow)
    : IExistsRoomTypeByNameUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        string name,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<bool>.ValidationFailure(
                ["Room type name is required."]);
        }

        name = name.Trim();

        if (name.Length > 100)
        {
            return Result<bool>.ValidationFailure(
                ["Room type name cannot exceed 100 characters."]);
        }

        bool exists = await uow.RoomTypeRepository.ExistsByNameAsync(
            name,
            ct);

        return Result<bool>.Success(exists);
    }
}