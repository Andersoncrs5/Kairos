using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Services.Providers;

public sealed class RemoveRoomFavoriteUseCase(IUnitOfWork uow)
    : IRemoveRoomFavoriteUseCase
{
    public async Task<Result<bool>> ExecuteAsync(
        RemoveRoomFavoriteCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<bool>.ValidationFailure(
                ["Favorite command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid favorite command.",
                out string[] errors))
        {
            return Result<bool>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifiers
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.RoomId,
                out string roomId))
        {
            return Result<bool>.ValidationFailure(
                ["Room ID must be a valid UUID."]);
        }

        if (!UseCaseValidation.TryParseUuid(
                command.UserId,
                out string userId))
        {
            return Result<bool>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Remove favorite (idempotent). The room is not looked up on
        //    purpose: the user can unfavorite rooms that were deactivated.
        // ---------------------------------------------------------------

        int deleted =
            await uow.RoomFavoriteRepository
                .DeleteByUserAndRoomAsync(userId, roomId, ct);

        // true: was removed now; false: it was not a favorite
        return Result<bool>.Success(deleted > 0);
    }
}