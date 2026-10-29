using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
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

[ApiController]
[Route("api/v{version:apiVersion}/rooms")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomController(
    IInboxTransactionUseCase<CreateRoomDto, RoomModel, ICreateRoomUseCase> createRoomUseCase,
    IInboxTransactionUseCase<string, bool, IDeleteRoomByIdUseCase> deleteRoomUseCase,
    IInboxTransactionUseCase<string, bool, IExistsRoomByCodeUseCase> existsRoomByCodeUseCase,
    IInboxTransactionUseCase<string, bool, IExistsRoomByIdUseCase> existsRoomByIdUseCase,
    IInboxTransactionUseCase<string, RoomModel, IFindRoomByIdUseCase> findRoomByIdUseCase,
    IInboxTransactionUseCase<UpdateRoomCommand, RoomModel, IUpdateRoomUseCase> updateRoomUseCase,
    IFindAllRoomUseCase findAllRooms
) : ControllerBase
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
    /// <param name="dto">Filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The rooms were retrieved successfully.</response>
    /// <response code="400">The filter parameters are invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to list rooms.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet]
    [RequirePermissions("rooms.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<RoomDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterRoomDto dto,
        CancellationToken ct)
    {
        Page<RoomModel> result =
            await findAllRooms.ExecuteAsync(dto, ct);

        Page<RoomDto> data = Page<RoomDto>.Create(
            result.Data
                .Select(RoomMapper.ToDto)
                .ToArray(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        return Ok(
            new ResponseHttp<Page<RoomDto>>(
                Data: data,
                Message: "Rooms retrieved successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room was found.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read rooms.</response>
    /// <response code="404">The room does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet("{id}")]
    [RequirePermissions("rooms.read")]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<RoomModel> result =
            await findRoomByIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomDto>(
                    Data: null,
                    Message: "Error finding room",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomDto room = RoomMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomDto>(
                Data: room,
                Message: "Room found",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by code
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room exists by its code.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="code">Code of the room to check.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The existence check succeeded.</response>
    /// <response code="400">The room code is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read rooms.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the check.</response>
    [HttpGet("Exists/code/{code}")]
    [RequirePermissions("rooms.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsByCode(
        [FromRoute, Required] string code,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result =
            await existsRoomByCodeUseCase.ExecuteAsync(
                inboxId,
                workerId,
                code,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking room code",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room exists"
                    : "Room does not exist",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room exists by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room to check.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The existence check succeeded.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read rooms.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the check.</response>
    [HttpGet("Exists/id/{id}")]
    [RequirePermissions("rooms.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result =
            await existsRoomByIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking room ID",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room exists"
                    : "Room does not exist",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new room.
    /// </summary>
    /// <remarks>
    /// Room codes must be unique.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="dto">Information required to create the room.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="201">The room was successfully created.</response>
    /// <response code="400">The request contains invalid data.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to create rooms.</response>
    /// <response code="404">The informed room type does not exist.</response>
    /// <response code="409">The room code already exists, or the Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented creation.</response>
    [HttpPost]
    [RequirePermissions("rooms.create")]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<RoomModel> result =
            await createRoomUseCase.ExecuteAsync(
                inboxId,
                workerId,
                dto,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomDto>(
                    Data: null,
                    Message: "Error creating room",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomDto room = RoomMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<RoomDto>(
                Data: room,
                Message: "Room created",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing room.
    /// </summary>
    /// <remarks>
    /// Only the supplied properties are changed; omitted properties keep
    /// their current values. Room codes must be unique.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room to update.</param>
    /// <param name="dto">Room properties to update.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room was successfully updated.</response>
    /// <response code="400">The identifier or request data is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to update rooms.</response>
    /// <response code="404">The room or the informed room type does not exist.</response>
    /// <response code="409">The room code already exists, or the Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the update.</response>
    [HttpPut("{id}")]
    [RequirePermissions("rooms.update")]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdateRoomDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        var command = new UpdateRoomCommand(id, dto);

        Result<RoomModel> result = await updateRoomUseCase.ExecuteAsync(inboxId, workerId, command, ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomDto>(
                    Data: null,
                    Message: "Error updating room",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomDto room = RoomMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomDto>(
                Data: room,
                Message: "Room updated successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room to delete.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room was successfully deleted.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to delete rooms.</response>
    /// <response code="404">The room does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented deletion.</response>
    [HttpDelete("{id}")]
    [RequirePermissions("rooms.delete")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result =
            await deleteRoomUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error deleting room",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: "Room deleted",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}