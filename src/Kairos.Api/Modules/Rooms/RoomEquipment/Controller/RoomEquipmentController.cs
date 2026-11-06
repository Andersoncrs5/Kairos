using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomEquipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomEquipment.Mapper;
using Kairos.Api.Modules.Rooms.RoomEquipment.Models;
using Kairos.Api.Modules.Rooms.RoomEquipment.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.RoomEquipment.Controller;

[ApiController]
[Route("api/v{version:apiVersion}/room-equipments")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomEquipmentController(
    IInboxTransactionUseCase<AddEquipmentToRoomCommand, RoomEquipmentModel, ICreateRoomEquipmentUseCase> createRoomEquipmentUseCase,
    IInboxTransactionUseCase<UpdateRoomEquipmentCommand, RoomEquipmentModel, IUpdateRoomEquipmentUseCase> updateRoomEquipmentUseCase,
    IInboxTransactionUseCase<string, bool, IDeleteRoomEquipmentUseCase> deleteRoomEquipmentUseCase,
    IFindRoomEquipmentByIdUseCase findRoomEquipmentByIdUseCase,
    IFindRoomEquipmentByRoomAndEquipmentUseCase findRoomEquipmentByRoomAndEquipmentUseCase,
    IFindRoomEquipmentsByRoomIdUseCase findRoomEquipmentsByRoomIdUseCase,
    IFindRoomEquipmentsUseCase findRoomEquipmentsUseCase,
    IExistsRoomEquipmentByRoomAndEquipmentUseCase existsRoomEquipmentByRoomAndEquipmentUseCase
) : ControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of room equipment associations.
    /// </summary>
    /// <remarks>
    /// Supports filtering, pagination and sort direction.
    /// Requires a valid JWT.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("rooms.equipments.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<RoomEquipmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindAll(
        [FromQuery] FilterRoomEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Page<RoomEquipmentModel> result =
            await findRoomEquipmentsUseCase.ExecuteAsync(dto, ct);

        Page<RoomEquipmentDto> data =
            Page<RoomEquipmentDto>.Create(
                result.Data
                    .Select(RoomEquipmentMapper.ToDto)
                    .ToArray(),
                result.TotalCount,
                result.PageNumber,
                result.PageSize);

        return Ok(
            new ResponseHttp<Page<RoomEquipmentDto>>(
                Data: data,
                Message: "Room equipments retrieved successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room equipment association by its identifier.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomEquipmentModel> result =
            await findRoomEquipmentByIdUseCase.ExecuteAsync(id, ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomEquipmentDto>(
                    Data: null,
                    Message: "Error finding room equipment",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomEquipmentDto roomEquipment =
            RoomEquipmentMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomEquipmentDto>(
                Data: roomEquipment,
                Message: "Room equipment found",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by Room ID
    // ------------------------------------------   ---------------------

    /// <summary>
    /// Retrieves all equipment associations belonging to a room.
    /// </summary>
    [HttpGet("rooms/{roomId}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<RoomEquipmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<RoomEquipmentDto>>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<RoomEquipmentDto>>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<RoomEquipmentDto>>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<RoomEquipmentDto>>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<RoomEquipmentDto>>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindByRoomId(
        [FromRoute] string roomId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<IReadOnlyList<RoomEquipmentModel>> result =
            await findRoomEquipmentsByRoomIdUseCase.ExecuteAsync(
                roomId,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<IReadOnlyList<RoomEquipmentDto>>(
                    Data: null,
                    Message: "Error retrieving room equipments",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        IReadOnlyList<RoomEquipmentDto> data =
            result.Value
                .Select(RoomEquipmentMapper.ToDto)
                .ToArray();

        return Ok(
            new ResponseHttp<IReadOnlyList<RoomEquipmentDto>>(
                Data: data,
                Message: "Room equipments retrieved successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by Room and Equipment
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves an equipment association using the room and equipment IDs.
    /// </summary>
    [HttpGet("rooms/{roomId}/equipments/{equipmentId}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindByRoomAndEquipment(
        [FromRoute] string roomId,
        [FromRoute] string equipmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomEquipmentModel> result =
            await findRoomEquipmentByRoomAndEquipmentUseCase.ExecuteAsync(
                roomId,
                equipmentId,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomEquipmentDto>(
                    Data: null,
                    Message: "Error finding room equipment association",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomEquipmentDto roomEquipment =
            RoomEquipmentMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomEquipmentDto>(
                Data: roomEquipment,
                Message: "Room equipment association found",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by Room and Equipment
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether an equipment is associated with a room.
    /// </summary>
    [HttpGet("exists/rooms/{roomId}/equipments/{equipmentId}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsByRoomAndEquipment(
        [FromRoute] string roomId,
        [FromRoute] string equipmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsRoomEquipmentByRoomAndEquipmentUseCase.ExecuteAsync(
                roomId,
                equipmentId,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking room equipment association",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Equipment is associated with this room"
                    : "Equipment is not associated with this room",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Associates an equipment with a room.
    /// </summary>
    /// <remarks>
    /// The room and equipment must exist.
    /// An equipment cannot be associated with the same room twice.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPost("rooms")]
    [RequirePermissions("rooms.equipments.create")]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] AddEquipmentToRoomDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        AddEquipmentToRoomCommand command =
            new AddEquipmentToRoomCommand(dto.RoomId, dto);

        Result<RoomEquipmentModel> result =
            await createRoomEquipmentUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomEquipmentDto>(
                    Data: null,
                    Message: "Error associating equipment with room",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomEquipmentDto roomEquipment =
            RoomEquipmentMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<RoomEquipmentDto>(
                Data: roomEquipment,
                Message: "Equipment associated with room successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing room equipment association.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("rooms.equipments.update")]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomEquipmentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdateRoomEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        UpdateRoomEquipmentCommand command =
            new UpdateRoomEquipmentCommand(id, dto);

        Result<RoomEquipmentModel> result =
            await updateRoomEquipmentUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomEquipmentDto>(
                    Data: null,
                    Message: "Error updating room equipment association",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomEquipmentDto roomEquipment =
            RoomEquipmentMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomEquipmentDto>(
                Data: roomEquipment,
                Message: "Room equipment association updated successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room equipment association by its identifier.
    /// </summary>
    /// <remarks>
    /// This removes the association, not the equipment itself.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpDelete("{id}")]
    [RequirePermissions("rooms.equipments.delete")]
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
            await deleteRoomEquipmentUseCase.ExecuteAsync(
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
                    Message: "Error deleting room equipment association",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: "Room equipment association deleted",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}
