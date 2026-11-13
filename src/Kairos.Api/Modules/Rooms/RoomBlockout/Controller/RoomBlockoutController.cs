using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Base.Controller;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Commands;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Request;
using Kairos.Api.Modules.Rooms.RoomBlockout.DTOs.Response;
using Kairos.Api.Modules.Rooms.RoomBlockout.Mapper;
using Kairos.Api.Modules.Rooms.RoomBlockout.Models;
using Kairos.Api.Modules.Rooms.RoomBlockout.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.RoomBlockout.Controller;

[Route("api/v{version:apiVersion}/room-blockouts")]
[ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomBlockoutController(
    IInboxTransactionUseCase<
        CreateRoomBlockoutCommand,
        RoomBlockoutModel,
        ICreateRoomBlockoutUseCase> createRoomBlockoutUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IDeleteRoomBlockoutByIdCase> deleteRoomBlockoutUseCase,

    IInboxTransactionUseCase<
        string,
        bool,
        IExistsRoomBlockoutByIdUseCase> existsRoomBlockoutByIdUseCase,

    IInboxTransactionUseCase<
        UpdateRoomBlockoutCommand,
        RoomBlockoutModel,
        IUpdateRoomBlockoutUseCase> updateRoomBlockoutUseCase,

    IFindRoomBlockoutUseCase findRoomBlockoutUseCase,

    IInboxTransactionUseCase<
        CancelRoomBlockoutCommand,
        RoomBlockoutModel,
        ICancelRoomBlockoutUseCase> cancelRoomBlockoutUseCase,

    IInboxTransactionUseCase<
        FilterRoomBlockoutDto,
        Page<RoomBlockoutModel>,
        IFindAllRoomBlockoutUseCase> findAllRoomBlockouts
) : ApiControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of room blockouts.
    /// </summary>
    /// <remarks>
    /// Supports filtering, pagination and sort direction.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("rooms.blockouts.list")]
    [ProducesResponseType(
        typeof(ResponseHttp<Page<RoomBlockoutDto>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<ValidationErrorResponse>),
        StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FindAll(
        [FromQuery] FilterRoomBlockoutDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<Page<RoomBlockoutModel>> result =
            await findAllRoomBlockouts.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                dto,
                ct);

        return ToResponse(
            result,
            ToPageDto,
            "Room blockouts retrieved successfully",
            "Error retrieving room blockouts",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a room blockout by its identifier.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermissions("rooms.blockouts.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomBlockoutDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomBlockoutDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FindById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomBlockoutModel> result =
            await findRoomBlockoutUseCase.ExecuteAsync(id, ct);

        return ToResponse(
            result,
            RoomBlockoutMapper.ToDto,
            "Room blockout found",
            "Error finding room blockout",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room blockout exists by its identifier.
    /// </summary>
    [HttpGet("exists/id/{id}")]
    [RequirePermissions("rooms.blockouts.read")]
    [ProducesResponseType(
        typeof(ResponseHttp<bool?>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ExistsById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<bool> result =
            await existsRoomBlockoutByIdUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToExistsResponse(
            result,
            "Room blockout exists",
            "Room blockout does not exist",
            "Error checking room blockout ID",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new room blockout.
    /// </summary>
    /// <remarks>
    /// The creator is identified through the authenticated user's JWT.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPost]
    [RequirePermissions("rooms.blockouts.create")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomBlockoutDto>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomBlockoutDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string createdBy = GetAuthenticatedUserId(inboxId);

        CreateRoomBlockoutCommand command = new()
        {
            Dto = dto,
            CreatedBy = createdBy
        };

        Result<RoomBlockoutModel> result =
            await createRoomBlockoutUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomBlockoutMapper.ToDto,
            "Room blockout created successfully",
            "Error creating room blockout",
            inboxId,
            StatusCodes.Status201Created);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing room blockout.
    /// </summary>
    /// <remarks>
    /// Only the supplied properties are changed.
    /// Cancelled room blockouts cannot be updated.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("rooms.blockouts.update")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomBlockoutDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<ValidationErrorResponse>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomBlockoutDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string id,
        [FromBody] UpdateRoomBlockoutDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateRoomBlockoutCommand command = new(id, dto);

        Result<RoomBlockoutModel> result =
            await updateRoomBlockoutUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomBlockoutMapper.ToDto,
            "Room blockout updated successfully",
            "Error updating room blockout",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Cancel
    // ---------------------------------------------------------------

    /// <summary>
    /// Cancels an existing room blockout.
    /// </summary>
    /// <remarks>
    /// The cancellation actor is identified through the authenticated
    /// user's JWT. The cancellation reason must be supplied in the body.
    /// </remarks>
    [HttpPost("{id}/cancel")]
    [RequirePermissions("rooms.blockouts.cancel")]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomBlockoutDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ResponseHttp<RoomBlockoutDto>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        [FromRoute, Required] string id,
        [FromBody] CancelRoomBlockoutDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string cancelledBy = GetAuthenticatedUserId(inboxId);

        CancelRoomBlockoutCommand command = new(
            RoomBlockoutId: id,
            CancellationReason: dto.CancellationReason,
            CancelledBy: cancelledBy);

        Result<RoomBlockoutModel> result =
            await cancelRoomBlockoutUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                command,
                ct);

        return ToResponse(
            result,
            RoomBlockoutMapper.ToDto,
            "Room blockout cancelled successfully",
            "Error cancelling room blockout",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room blockout by its identifier.
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermissions("rooms.blockouts.delete")]
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
            await deleteRoomBlockoutUseCase.ExecuteAsync(
                inboxId,
                NewWorkerId(),
                id,
                ct);

        return ToResponse(
            result,
            value => (bool?)value,
            "Room blockout deleted successfully",
            "Error deleting room blockout",
            inboxId);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static Page<RoomBlockoutDto> ToPageDto(
        Page<RoomBlockoutModel> page)
    {
        return Page<RoomBlockoutDto>.Create(
            page.Data
                .Select(RoomBlockoutMapper.ToDto)
                .ToArray(),
            page.TotalCount,
            page.PageNumber,
            page.PageSize);
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
