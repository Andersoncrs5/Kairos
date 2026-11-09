using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Services.Providers;

public sealed class FindRoomFavoritesUseCase(IUnitOfWork uow)
    : IFindRoomFavoritesUseCase
{
    public async Task<Result<Page<RoomFavoriteModel>>> ExecuteAsync(
        FindRoomFavoritesCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<Page<RoomFavoriteModel>>.ValidationFailure(
                ["Favorite search command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid favorite search command.",
                out string[] errors))
        {
            return Result<Page<RoomFavoriteModel>>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate filter
        // ---------------------------------------------------------------

        FilterRoomFavoriteDto dto = command.Dto;

        if (!UseCaseValidation.TryValidate(
                dto,
                "Invalid favorite filter.",
                out errors))
        {
            return Result<Page<RoomFavoriteModel>>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 3. Validate requester
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.RequesterUserId,
                out string requesterUserId))
        {
            return Result<Page<RoomFavoriteModel>>.ValidationFailure(
                ["Requester user ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 4. Scope: users only see their own favorites
        // ---------------------------------------------------------------

        if (!string.IsNullOrWhiteSpace(dto.UserId))
        {
            if (!UseCaseValidation.TryParseUuid(
                    dto.UserId,
                    out string filterUserId))
            {
                return Result<Page<RoomFavoriteModel>>.ValidationFailure(
                    ["User ID must be a valid UUID."]);
            }

            if (filterUserId != requesterUserId)
            {
                return Result<Page<RoomFavoriteModel>>.Forbidden(
                    "You can only list your own favorite rooms.");
            }
        }

        // UserId is settable in the filter: always the authenticated user
        dto.UserId = requesterUserId;

        // ---------------------------------------------------------------
        // 5. Find favorites
        // ---------------------------------------------------------------

        Page<RoomFavoriteModel> favorites =
            await uow.RoomFavoriteRepository.Find(dto, ct);

        return Result<Page<RoomFavoriteModel>>.Success(favorites);
    }
}