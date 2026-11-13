
using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
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

[Route("api/v{version:apiVersion}/room-equipments")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomEquipmentController(
    IInboxTransactionUseCase<
        AddEquipmentToRoomCommand,
        RoomEquipmentModel,
        ICreateRoomEquipmentUseCase> createRoomEquipmentUseCase,

    IInboxTransactionUseCase<
        UpdateRoomEquipmentCommand,
        RoomEquipmentModel,
        IUpdateRoomEquipmentUseCase> updateRoomEquipmentUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IDeleteRoomEquipmentUseCase> deleteRoomEquipmentUseCase,

    IFindRoomEquipmentByIdUseCase findRoomEquipmentByIdUseCase,

    IFindRoomEquipmentByRoomAndEquipmentUseCase findRoomEquipmentByRoomAndEquipmentUseCase,

    IFindRoomEquipmentsByRoomIdUseCase findRoomEquipmentsByRoomIdUseCase,

    IFindRoomEquipmentsUseCase findRoomEquipmentsUseCase,

    IExistsRoomEquipmentByRoomAndEquipmentUseCase existsRoomEquipmentByRoomAndEquipmentUseCase
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of room equipment associations.
    /// </summary>
    /// <remarks>
    /// Supports filtering, pagination and sort direction.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("rooms.equipments.list")]
    [ProducesResponseType(
        typeof(ResponseHttp<Page<RoomEquipmentDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> FindAll(
        [FromQuery] FilterRoomEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Page<RoomEquipmentModel> result =
            await findRoomEquipmentsUseCase.ExecuteAsync(dto, ct);

        Page<RoomEquipmentDto> data = ToPageDto(result);

        return SuccessResponse(
            data,
            "Room equipments retrieved successfully",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room equipment association by its identifier.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomEquipmentDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomEquipmentDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomEquipmentModel> result =
            await findRoomEquipmentByIdUseCase.ExecuteAsync(
                id,
                ct);

        return ToResponse(
            result,
            RoomEquipmentMapper.ToDto,
            "Room equipment found",
            "Error finding room equipment",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by Room ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves all equipment associations belonging to a room.
    /// </summary>
    [HttpGet("rooms/{roomId}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<IReadOnlyList<RoomEquipmentDto>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> FindByRoomId(
        [FromRoute, Required] string roomId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<IReadOnlyList<RoomEquipmentModel>> result =
            await findRoomEquipmentsByRoomIdUseCase.ExecuteAsync(
                roomId,
                ct);

        return ToResponse(
            result,
            ToDtos,
            "Room equipments retrieved successfully",
            "Error retrieving room equipments",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by Room and Equipment
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves an equipment association using the room and equipment IDs.
    /// </summary>
    [HttpGet("rooms/{roomId}/equipments/{equipmentId}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomEquipmentDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomEquipmentDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindByRoomAndEquipment(
        [FromRoute, Required] string roomId,
        [FromRoute, Required] string equipmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomEquipmentModel> result =
            await findRoomEquipmentByRoomAndEquipmentUseCase.ExecuteAsync(
                roomId,
                equipmentId,
                ct);

        return ToResponse(
            result,
            RoomEquipmentMapper.ToDto,
            "Room equipment association found",
            "Error finding room equipment association",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Exists by Room and Equipment
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether an equipment is associated with a room.
    /// </summary>
    [HttpGet("exists/rooms/{roomId}/equipments/{equipmentId}")]
    [RequirePermissions("rooms.equipments.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ExistsByRoomAndEquipment(
        [FromRoute, Required] string roomId,
        [FromRoute, Required] string equipmentId,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsRoomEquipmentByRoomAndEquipmentUseCase.ExecuteAsync(
                roomId,
                equipmentId,
                ct);

        return ToExistsResponse(
            result,
            "Equipment is associated with this room",
            "Equipment is not associated with this room",
            "Error checking room equipment association",
            inboxId);
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
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPost("rooms")]
    [RequirePermissions("rooms.equipments.create")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomEquipmentDto>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] AddEquipmentToRoomDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        AddEquipmentToRoomCommand command =
            new(dto.RoomId, dto);

        Result<RoomEquipmentModel> result =
            await createRoomEquipmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomEquipmentMapper.ToDto,
            "Equipment associated with room successfully",
            "Error associating equipment with room",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing room equipment association.
    /// </summary>
    /// <remarks>
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("rooms.equipments.update")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomEquipmentDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomEquipmentDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string id,
        [FromBody] UpdateRoomEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateRoomEquipmentCommand command = new(id, dto);

        Result<RoomEquipmentModel> result =
            await updateRoomEquipmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomEquipmentMapper.ToDto,
            "Room equipment association updated successfully",
            "Error updating room equipment association",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room equipment association by its identifier.
    /// </summary>
    /// <remarks>
    /// This removes the association, not the equipment itself.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpDelete("{id}")]
    [RequirePermissions("rooms.equipments.delete")]
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
            await deleteRoomEquipmentUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            "Room equipment association deleted successfully",
            "Error deleting room equipment association",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<RoomEquipmentDto> ToPageDto(
        Page<RoomEquipmentModel> page)
    {
        return Page<RoomEquipmentDto>.Create(
            page.Data
                .Select(RoomEquipmentMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
    }

    private static IReadOnlyList<RoomEquipmentDto> ToDtos(
        IReadOnlyList<RoomEquipmentModel> models)
    {
        return models
            .Select(RoomEquipmentMapper.ToDto)
            .ToArray();
    }

    private ObjectResult ToExistsResponse(
        Result<bool> result,
        string existsMessage,
        string notExistsMessage,
        string errorMessage,
        string inboxId)
    {
        if (result.IsFailure)
        {
            return FailureResponse<bool?>(
                result.StatusCode,
                result.Errors,
                errorMessage,
                inboxId);
        }

        return SuccessResponse(
            (bool?)result.Value,
            result.Value ? existsMessage : notExistsMessage,
            inboxId);
    }
}
