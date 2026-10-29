using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomTypes.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomTypes.Mapper;
using Kairos.Api.Modules.Rooms.RoomTypes.Models;
using Kairos.Api.Modules.Rooms.RoomTypes.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.RoomTypes.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/room-types")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomTypeController(
    IInboxTransactionUseCase<CreateRoomTypeDto, RoomTypeModel, ICreateRoomTypeUseCase> createRoomTypeUseCase,
    IInboxTransactionUseCase<string, bool, IDeleteRoomTypeUseCase> deleteRoomTypeUseCase,
    IInboxTransactionUseCase<string, bool, IExistsRoomTypeByNameUseCase> existsRoomTypeByNameUseCase,
    IInboxTransactionUseCase<string, bool, IExistsRoomTypeByIdUseCase> existsRoomTypeByIdUseCase,
    IInboxTransactionUseCase<string, RoomTypeModel, IFindRoomTypeUseCase> findRoomTypeUseCase,
    IInboxTransactionUseCase<UpdateRoomTypeCommand, RoomTypeModel, IUpdateRoomTypeUseCase> updateRoomTypeUseCase,
    IFindAllRoomTypeUseCase findAllRoomTypes
) : ControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of room types.
    /// </summary>
    /// <remarks>
    /// Supports filtering by ID, version, creation date, update date,
    /// name, description and active status.
    /// Supports pagination and sort direction.
    /// Requires a valid JWT and the room-types.read permission.
    /// </remarks>
    /// <param name="dto">Filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room types were retrieved successfully.</response>
    /// <response code="400">The filter parameters are invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read room types.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet]
    [RequirePermissions("room-types.read")]
    [ProducesResponseType(typeof(ResponseHttp<Page<RoomTypeDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterRoomTypeDto dto,
        CancellationToken ct)
    {
        Page<RoomTypeModel> result =
            await findAllRoomTypes.ExecuteAsync(dto, ct);

        Page<RoomTypeDto> data = Page<RoomTypeDto>.Create(
            result.Data
                .Select(RoomTypeMapper.ToDto)
                .ToArray(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        return Ok(
            new ResponseHttp<Page<RoomTypeDto>>(
                Data: data,
                Message: "Room types retrieved successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room type by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room type.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room type was found.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read room types.</response>
    /// <response code="404">The room type does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet("{id}")]
    [RequirePermissions("room-types.read")]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<RoomTypeModel> result =
            await findRoomTypeUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomTypeDto>(
                    Data: null,
                    Message: "Error finding room type",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomTypeDto roomType = RoomTypeMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomTypeDto>(
                Data: roomType,
                Message: "Room type found",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by name
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room type exists by its name.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="name">Name of the room type to check.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The existence check succeeded.</response>
    /// <response code="400">The room type name is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read room types.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the check.</response>
    [HttpGet("Exists/name/{name}")]
    [RequirePermissions("room-types.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsByName(
        [FromRoute, Required] string name,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result =
            await existsRoomTypeByNameUseCase.ExecuteAsync(
                inboxId,
                workerId,
                name,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking room type name",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room type exists"
                    : "Room type does not exist",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room type exists by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room type to check.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The existence check succeeded.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read room types.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the check.</response>
    [HttpGet("Exists/id/{id}")]
    [RequirePermissions("room-types.read")]
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
            await existsRoomTypeByIdUseCase.ExecuteAsync(
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
                    Message: "Error checking room type ID",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room type exists"
                    : "Room type does not exist",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new room type.
    /// </summary>
    /// <remarks>
    /// Room type names must be unique.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="dto">Information required to create the room type.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="201">The room type was successfully created.</response>
    /// <response code="400">The request contains invalid data.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to create room types.</response>
    /// <response code="409">The room type name already exists, or the Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented creation.</response>
    [HttpPost]
    [RequirePermissions("room-types.create")]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomTypeDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<RoomTypeModel> result =
            await createRoomTypeUseCase.ExecuteAsync(
                inboxId,
                workerId,
                dto,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomTypeDto>(
                    Data: null,
                    Message: "Error creating room type",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomTypeDto roomType = RoomTypeMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<RoomTypeDto>(
                Data: roomType,
                Message: "Room type created",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing room type.
    /// </summary>
    /// <remarks>
    /// Room type names must be unique.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room type to update.</param>
    /// <param name="dto">Room type properties to update.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room type was successfully updated.</response>
    /// <response code="400">The identifier or request data is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to update room types.</response>
    /// <response code="404">The room type does not exist.</response>
    /// <response code="409">The room type name already exists, or the Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented the update.</response>
    [HttpPut("{id}")]
    [RequirePermissions("room-types.update")]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomTypeDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdateRoomTypeDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        var command = new UpdateRoomTypeCommand(id, dto);

        Result<RoomTypeModel> result =
            await updateRoomTypeUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomTypeDto>(
                    Data: null,
                    Message: "Error updating room type",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomTypeDto roomType = RoomTypeMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomTypeDto>(
                Data: roomType,
                Message: "Room type updated successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room type by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room type to delete.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room type was successfully deleted.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to delete room types.</response>
    /// <response code="404">The room type does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented deletion.</response>
    [HttpDelete("{id}")]
    [RequirePermissions("room-types.delete")]
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
            await deleteRoomTypeUseCase.ExecuteAsync(
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
                    Message: "Error deleting room type",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: "Room type deleted",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}