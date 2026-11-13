using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.Room.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Room.DTOs.Request;
using Kairos.Api.Modules.Rooms.Room.DTOs.Response;
using Kairos.Api.Modules.Rooms.Room.Mapper;
using Kairos.Api.Modules.Rooms.Room.Models;
using Kairos.Api.Modules.Rooms.Room.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.Room.Controllers;

[Route("api/v{version:apiVersion}/rooms")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomController(
    IInboxTransactionUseCase<
        CreateRoomDto,
        RoomModel,
        ICreateRoomUseCase> createRoomUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IDeleteRoomByIdUseCase> deleteRoomUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IExistsRoomByCodeUseCase> existsRoomByCodeUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IExistsRoomByIdUseCase> existsRoomByIdUseCase,

    IInboxTransactionUseCase<
        string,
        RoomModel,
        IFindRoomByIdUseCase> findRoomByIdUseCase,

    IInboxTransactionUseCase<
        UpdateRoomCommand,
        RoomModel,
        IUpdateRoomUseCase> updateRoomUseCase,

    IFindAllRoomUseCase findAllRooms
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of rooms.
    /// </summary>
    /// <remarks>
    /// Supports filtering, pagination and sort direction.
    /// Requires a valid JWT and the rooms.list permission.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("rooms.list")]
    [ProducesResponseType(
        typeof(ResponseHttp<Page<RoomDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterRoomDto dto,
        CancellationToken ct)
    {
        Page<RoomModel> result =
            await findAllRooms.ExecuteAsync(dto, ct);

        Page<RoomDto> data = ToPageDto(result);

        return SuccessResponse(
            data,
            "Rooms retrieved successfully",
            HttpContext.TraceIdentifier);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room by its identifier.
    /// </summary>
    /// <remarks>
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpGet("{id}")]
    [RequirePermissions("rooms.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomModel> result =
            await findRoomByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            RoomMapper.ToDto,
            "Room found",
            "Error finding room",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Exists by code
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room exists by its code.
    /// </summary>
    [HttpGet("Exists/code/{code}")]
    [RequirePermissions("rooms.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ExistsByCode(
        [FromRoute, Required] string code,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsRoomByCodeUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                code,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            result.IsSuccess && result.Value
                ? "Room exists"
                : "Room does not exist",
            "Error checking room code",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room exists by its identifier.
    /// </summary>
    [HttpGet("Exists/id/{id}")]
    [RequirePermissions("rooms.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ExistsById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsRoomByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            result.IsSuccess && result.Value
                ? "Room exists"
                : "Room does not exist",
            "Error checking room ID",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new room.
    /// </summary>
    /// <remarks>
    /// Room codes must be unique.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPost]
    [RequirePermissions("rooms.create")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomDto>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomModel> result =
            await createRoomUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                dto,
                ct);

        return ToResponse(
            result,
            RoomMapper.ToDto,
            "Room created successfully",
            "Error creating room",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing room.
    /// </summary>
    /// <remarks>
    /// Only the supplied properties are changed.
    /// Room codes must be unique.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("rooms.update")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string id,
        [FromBody] UpdateRoomDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateRoomCommand command = new(id, dto);

        Result<RoomModel> result =
            await updateRoomUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomMapper.ToDto,
            "Room updated successfully",
            "Error updating room",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room by its identifier.
    /// </summary>
    /// <remarks>
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpDelete("{id}")]
    [RequirePermissions("rooms.delete")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await deleteRoomUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            "Room deleted successfully",
            "Error deleting room",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<RoomDto> ToPageDto(
        Page<RoomModel> page)
    {
        return Page<RoomDto>.Create(
            page.Data
                .Select(RoomMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }
}
