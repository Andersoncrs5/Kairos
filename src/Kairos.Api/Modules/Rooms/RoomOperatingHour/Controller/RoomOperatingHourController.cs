using System.ComponentModel.DataAnnotations;
using System.Net;
using Asp.Versioning;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Mapper;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Model;
using Kairos.Api.Modules.Rooms.RoomOperatingHour.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.RoomOperatingHour.Controller;

[ApiController]
[Route("api/v{version:apiVersion}/room-operating-hours")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomOperatingHourController(
    IInboxTransactionUseCase<CreateRoomOperatingHourCommand, RoomOperatingHourModel, ICreateRoomOperatingHourUseCase> createRoomOperatingHourUseCase,
    IInboxTransactionUseCase<string, bool, IDeleteRoomOperatingHourUseCase> deleteRoomOperatingHourUseCase,
    IInboxTransactionUseCase<string, bool, IExistsRoomOperatingHourUseCase> existsRoomOperatingHourUseCase,
    IInboxTransactionUseCase<UpdateRoomOperatingHourCommand, RoomOperatingHourModel, IUpdateRoomOperatingHourUseCase> updateRoomOperatingHourUseCase,
    IInboxTransactionUseCase<string, RoomOperatingHourModel, IFindRoomOperatingHourByIdUseCase> findRoomOperatingHourByIdUseCase,
    IInboxTransactionUseCase<string, IReadOnlyList<RoomOperatingHourModel>, IFindRoomOperatingHoursByRoomIdUseCase> findRoomOperatingHoursByRoomIdUseCase,
    IInboxTransactionUseCase<FilterRoomOperatingHourDto, Page<RoomOperatingHourModel>, IFindRoomOperatingHourService> findRoomOperatingHours
) : ControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of room operating hours.
    /// </summary>
    /// <remarks>
    /// Supports filtering, pagination and sort direction.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("rooms.operating-hours.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<RoomOperatingHourDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindAll(
        [FromQuery] FilterRoomOperatingHourDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<Page<RoomOperatingHourModel>> result =
            await findRoomOperatingHours.ExecuteAsync(
                inboxId,
                workerId,
                dto,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<Page<RoomOperatingHourDto>>(
                    Data: null,
                    Message: "Error retrieving room operating hours",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        Page<RoomOperatingHourDto> data =
            Page<RoomOperatingHourDto>.Create(
                result.Value.Data
                    .Select(RoomOperatingHourMapper.ToDto)
                    .ToArray(),
                result.Value.TotalCount,
                result.Value.PageNumber,
                result.Value.PageSize);

        return Ok(
            new ResponseHttp<Page<RoomOperatingHourDto>>(
                Data: data,
                Message: "Room operating hours retrieved successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room operating hour by its identifier.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermissions("rooms.operating-hours.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomOperatingHourDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<RoomOperatingHourModel> result =
            await findRoomOperatingHourByIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomOperatingHourDto>(
                    Data: null,
                    Message: "Error finding room operating hour",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomOperatingHourDto data =
            RoomOperatingHourMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomOperatingHourDto>(
                Data: data,
                Message: "Room operating hour found",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by room ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves all operating hours configured for a room.
    /// </summary>
    [HttpGet("rooms/{roomId}")]
    [RequirePermissions("rooms.operating-hours.read")]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<RoomOperatingHourDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindByRoomId(
        [FromRoute, Required] string roomId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<IReadOnlyList<RoomOperatingHourModel>> result =
            await findRoomOperatingHoursByRoomIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                roomId,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<IReadOnlyList<RoomOperatingHourDto>>(
                    Data: null,
                    Message: "Error retrieving room operating hours",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        IReadOnlyList<RoomOperatingHourDto> data =
            result.Value
                .Select(RoomOperatingHourMapper.ToDto)
                .ToArray();

        return Ok(
            new ResponseHttp<IReadOnlyList<RoomOperatingHourDto>>(
                Data: data,
                Message: "Room operating hours retrieved successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room operating hour exists.
    /// </summary>
    [HttpGet("exists/id/{id}")]
    [RequirePermissions("rooms.operating-hours.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result =
            await existsRoomOperatingHourUseCase.ExecuteAsync(
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
                    Message: "Error checking room operating hour ID",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room operating hour exists"
                    : "Room operating hour does not exist",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new operating hour configuration for a room.
    /// </summary>
    [HttpPost("rooms/{roomId}")]
    [RequirePermissions("rooms.operating-hours.create")]
    [ProducesResponseType(typeof(ResponseHttp<RoomOperatingHourDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromRoute, Required] string roomId,
        [FromBody] CreateRoomOperatingHourDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        CreateRoomOperatingHourCommand command =
            new(roomId, dto);

        Result<RoomOperatingHourModel> result =
            await createRoomOperatingHourUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomOperatingHourDto>(
                    Data: null,
                    Message: "Error creating room operating hour",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomOperatingHourDto data =
            RoomOperatingHourMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<RoomOperatingHourDto>(
                Data: data,
                Message: "Room operating hour created",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing room operating hour.
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermissions("rooms.operating-hours.update")]
    [ProducesResponseType(typeof(ResponseHttp<RoomOperatingHourDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string id,
        [FromBody] UpdateRoomOperatingHourDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        UpdateRoomOperatingHourCommand command =
            new(id, dto);

        Result<RoomOperatingHourModel> result =
            await updateRoomOperatingHourUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomOperatingHourDto>(
                    Data: null,
                    Message: "Error updating room operating hour",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomOperatingHourDto data =
            RoomOperatingHourMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomOperatingHourDto>(
                Data: data,
                Message: "Room operating hour updated successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room operating hour by its identifier.
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermissions("rooms.operating-hours.delete")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result =
            await deleteRoomOperatingHourUseCase.ExecuteAsync(
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
                    Message: "Error deleting room operating hour",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room operating hour deleted"
                    : "Room operating hour does not exist",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}