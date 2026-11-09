using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomFavorite.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomFavorite.Mapper;
using Kairos.Api.Modules.Rooms.RoomFavorite.Models;
using Kairos.Api.Modules.Rooms.RoomFavorite.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.RoomFavorite.Controller;

[Route("api/v{version:apiVersion}/rooms")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomFavoriteController(
    IInboxTransactionUseCase<AddRoomFavoriteCommand, RoomFavoriteModel, IAddRoomFavoriteUseCase> addFavoriteUseCase,
    IInboxTransactionUseCase<RemoveRoomFavoriteCommand, bool, IRemoveRoomFavoriteUseCase> removeFavoriteUseCase,
    IInboxTransactionUseCase<FindRoomFavoritesCommand, Page<RoomFavoriteModel>, IFindRoomFavoritesUseCase> findFavoritesUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Add (favorite a room)
    // ---------------------------------------------------------------

    /// <summary>
    /// Adds a room to the favorites of the authenticated user.
    /// </summary>
    /// <remarks>
    /// Idempotent: favoriting a room that is already a favorite returns
    /// the existing favorite. The user is identified through the
    /// authenticated JWT. The request has no body.
    /// Requires the X-Inbox-Id header.
    /// </remarks>
    [HttpPut("{roomId}/favorite")]
    [RequirePermissions("rooms.favorites.create")]
    [ProducesResponseType(typeof(ResponseHttp<RoomFavoriteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(
        [FromRoute, Required] string roomId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        AddRoomFavoriteCommand command = new(
            RoomId: roomId,
            UserId: GetAuthenticatedUserId(inboxId));

        Result<RoomFavoriteModel> result =
            await addFavoriteUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomFavoriteMapper.ToDto,
            "Room added to favorites successfully",
            "Error adding room to favorites",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Remove (unfavorite a room)
    // ---------------------------------------------------------------

    /// <summary>
    /// Removes a room from the favorites of the authenticated user.
    /// </summary>
    /// <remarks>
    /// Idempotent: the response data is true when the favorite was removed
    /// now, and false when the room was not a favorite.
    /// Rooms that were deactivated can still be removed.
    /// </remarks>
    [HttpDelete("{roomId}/favorite")]
    [RequirePermissions("rooms.favorites.delete")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Remove(
        [FromRoute, Required] string roomId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        RemoveRoomFavoriteCommand command = new(
            RoomId: roomId,
            UserId: GetAuthenticatedUserId(inboxId));

        Result<bool> result =
            await removeFavoriteUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            removed => (bool?)removed,
            "Room is no longer a favorite",
            "Error removing room from favorites",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find my favorites
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of the favorite rooms
    /// of the authenticated user.
    /// </summary>
    /// <remarks>
    /// Supports filtering by room code and name, pagination and sorting.
    /// Users can only list their own favorites.
    /// </remarks>
    [HttpGet("favorites/me")]
    [RequirePermissions("rooms.favorites.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<RoomFavoriteDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> FindMine(
        [FromQuery] FilterRoomFavoriteDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        // O UserId do filtro NÃO é sobrescrito aqui: se o cliente enviar o id
        // de outra pessoa, o use case devolve 403 em vez de ignorar em silêncio.
        FindRoomFavoritesCommand command = new(
            RequesterUserId: GetAuthenticatedUserId(inboxId),
            Dto: dto);

        Result<Page<RoomFavoriteModel>> result =
            await findFavoritesUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            ToPageDto,
            "Favorite rooms retrieved successfully",
            "Error retrieving favorite rooms",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<RoomFavoriteDto> ToPageDto(
        Page<RoomFavoriteModel> page)
    {
        return Page<RoomFavoriteDto>.Create(
            page.Data
                .Select(RoomFavoriteMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}