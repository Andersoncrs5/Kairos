using Kairos.Api.Common.Result;
using Kairos.Api.Common.Validation;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.Mapper;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Services.Interfaces;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Services.Providers;

public sealed class AddRoomFavoriteUseCase(IUnitOfWork uow)
    : IAddRoomFavoriteUseCase
{
    private const int MaxFavoritesPerUser = 50;

    public async Task<Result<RoomFavoriteModel>> ExecuteAsync(
        AddRoomFavoriteCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // ---------------------------------------------------------------
        // 1. Validate command
        // ---------------------------------------------------------------

        if (command is null)
        {
            return Result<RoomFavoriteModel>.ValidationFailure(
                ["Favorite command is required."]);
        }

        if (!UseCaseValidation.TryValidate(
                command,
                "Invalid favorite command.",
                out string[] errors))
        {
            return Result<RoomFavoriteModel>.ValidationFailure(errors);
        }

        // ---------------------------------------------------------------
        // 2. Validate identifiers
        // ---------------------------------------------------------------

        if (!UseCaseValidation.TryParseUuid(
                command.RoomId,
                out string roomId))
        {
            return Result<RoomFavoriteModel>.ValidationFailure(
                ["Room ID must be a valid UUID."]);
        }

        if (!UseCaseValidation.TryParseUuid(
                command.UserId,
                out string userId))
        {
            return Result<RoomFavoriteModel>.ValidationFailure(
                ["User ID must be a valid UUID."]);
        }

        // ---------------------------------------------------------------
        // 3. Find room
        // ---------------------------------------------------------------

        RoomModel? room =
            await uow.RoomRepository.GetByIdAsync(roomId, ct);

        if (room is null)
        {
            return Result<RoomFavoriteModel>.NotFound(
                "Room doesn't exist.");
        }

        if (!room.IsActive)
        {
            return Result<RoomFavoriteModel>.BadRequest(
                "Room is not active.");
        }

        // ---------------------------------------------------------------
        // 4. Already a favorite? (idempotent: returns the existing one)
        // ---------------------------------------------------------------

        RoomFavoriteModel? existing =
            await uow.RoomFavoriteRepository
                .GetByUserAndRoomAsync(userId, roomId, ct);

        if (existing is not null)
        {
            existing.Room = room;

            return Result<RoomFavoriteModel>.Success(existing);
        }

        // ---------------------------------------------------------------
        // 5. Check favorites limit
        // ---------------------------------------------------------------

        int currentCount =
            await uow.RoomFavoriteRepository.CountByUserAsync(userId, ct);

        if (currentCount >= MaxFavoritesPerUser)
        {
            return Result<RoomFavoriteModel>.Conflict(
                $"Favorite limit reached ({MaxFavoritesPerUser} rooms).");
        }

        // ---------------------------------------------------------------
        // 6. Create favorite
        // ---------------------------------------------------------------

        RoomFavoriteModel favorite = RoomFavoriteMapper.ToModel(
            Guid.NewGuid().ToString(),
            userId,
            roomId,
            DateTimeOffset.UtcNow);

        favorite.Room = room;

        // ---------------------------------------------------------------
        // 7. Persist favorite (SaveChanges is done by the transaction)
        // ---------------------------------------------------------------

        await uow.RoomFavoriteRepository.AddAsync(favorite, ct);

        return Result<RoomFavoriteModel>.Success(favorite);
    }
}