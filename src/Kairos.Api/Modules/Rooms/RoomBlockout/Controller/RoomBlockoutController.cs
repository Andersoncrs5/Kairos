using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using Kairos.Api.Common.Exceptions;
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

[ApiController]
[Route("api/v{version:apiVersion}/room-blockouts")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class RoomBlockoutController(
    IInboxTransactionUseCase<CreateRoomBlockoutCommand, RoomBlockoutModel, ICreateRoomBlockoutUseCase> createRoomBlockoutUseCase,
    IInboxTransactionUseCase<string, bool, IDeleteRoomBlockoutByIdCase> deleteRoomBlockoutUseCase,
    IInboxTransactionUseCase<string, bool, IExistsRoomBlockoutByIdUseCase> existsRoomBlockoutByIdUseCase,
    IInboxTransactionUseCase<UpdateRoomBlockoutCommand, RoomBlockoutModel, IUpdateRoomBlockoutUseCase> updateRoomBlockoutUseCase,
    IFindRoomBlockoutUseCase findRoomBlockoutUseCase,
    IInboxTransactionUseCase<CancelRoomBlockoutCommand, RoomBlockoutModel, ICancelRoomBlockoutUseCase> cancelRoomBlockoutUseCase
) : ControllerBase
{
    /// <summary>
    /// Retrieves a room blockout by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT.
    /// </remarks>
    /// <param name="id">Identifier of the room blockout.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room blockout was found.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read room blockouts.</response>
    /// <response code="404">The room blockout does not exist.</response>
    /// <response code="500">An internal error prevented the query.</response>
    [HttpGet("{id}")]
    [RequirePermissions("rooms.blockouts.read")]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Result<RoomBlockoutModel> result =
            await findRoomBlockoutUseCase.ExecuteAsync(id, ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomBlockoutDto>(
                    Data: null,
                    Message: "Error finding room blockout",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomBlockoutDto roomBlockout =
            RoomBlockoutMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomBlockoutDto>(
                Data: roomBlockout,
                Message: "Room blockout found",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }


    /// <summary>
    /// Checks whether a room blockout exists by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT.
    /// </remarks>
    /// <param name="id">Identifier of the room blockout.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The existence check succeeded.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to read room blockouts.</response>
    /// <response code="500">An internal error prevented the check.</response>
    [HttpGet("exists/id/{id}")]
    [RequirePermissions("rooms.blockouts.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsById(
        [FromRoute, Required] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<bool> result =
            await existsRoomBlockoutByIdUseCase.ExecuteAsync(
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
                    Message: "Error checking room blockout ID",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room blockout exists"
                    : "Room blockout does not exist",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new room blockout.
    /// </summary>
    /// <remarks>
    /// The creator is identified through the authenticated user's JWT.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="dto">Information required to create the room blockout.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="201">The room blockout was successfully created.</response>
    /// <response code="400">The request contains invalid data.</response>
    /// <response code="401">Authentication is required or the user ID is unavailable.</response>
    /// <response code="403">The user lacks permission to create room blockouts.</response>
    /// <response code="404">The informed room does not exist.</response>
    /// <response code="409">The room blockout overlaps an existing blockout, or the Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented creation.</response>
    [HttpPost]
    [RequirePermissions("rooms.blockouts.create")]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomBlockoutDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string createdBy = GetAuthenticatedUserId();
        string workerId = $"http-{Guid.NewGuid():N}";

        CreateRoomBlockoutCommand command = new CreateRoomBlockoutCommand
        {
            Dto = dto,
            CreatedBy = createdBy
        };

        Result<RoomBlockoutModel> result =
            await createRoomBlockoutUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomBlockoutDto>(
                    Data: null,
                    Message: "Error creating room blockout",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomBlockoutDto roomBlockout =
            RoomBlockoutMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<RoomBlockoutDto>(
                Data: roomBlockout,
                Message: "Room blockout created",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
    
    /// <summary>
    /// Updates an existing room blockout.
    /// </summary>
    /// <remarks>
    /// Only the supplied properties are changed.
    /// Omitted properties keep their current values.
    /// Cancelled room blockouts cannot be updated.
    /// Requires a valid JWT.
    /// </remarks>
    /// <param name="id">Identifier of the room blockout to update.</param>
    /// <param name="dto">Room blockout properties to update.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room blockout was successfully updated.</response>
    /// <response code="400">The identifier or request data is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to update room blockouts.</response>
    /// <response code="404">The room blockout or informed room does not exist.</response>
    /// <response code="409">The room blockout overlaps an existing blockout.</response>
    /// <response code="500">An internal error prevented the update.</response>
    [HttpPut("{id}")]
    [RequirePermissions("rooms.blockouts.update")]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdateRoomBlockoutDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        UpdateRoomBlockoutCommand command = new UpdateRoomBlockoutCommand(id, dto);
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<RoomBlockoutModel> result =
            await updateRoomBlockoutUseCase.ExecuteAsync(
                inboxId: inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomBlockoutDto>(
                    Data: null,
                    Message: "Error updating room blockout",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomBlockoutDto roomBlockout =
            RoomBlockoutMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomBlockoutDto>(
                Data: roomBlockout,
                Message: "Room blockout updated successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Cancel
    // ---------------------------------------------------------------

    /// <summary>
    /// Cancels an existing room blockout.
    /// </summary>
    /// <remarks>
    /// The cancellation actor is identified through the authenticated user's JWT.
    /// The cancellation reason must be supplied in the request body.
    /// Requires a valid JWT.
    /// </remarks>
    /// <param name="id">Identifier of the room blockout to cancel.</param>
    /// <param name="dto">Cancellation reason.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room blockout was successfully cancelled.</response>
    /// <response code="400">The identifier or cancellation reason is invalid.</response>
    /// <response code="401">Authentication is required or the user ID is unavailable.</response>
    /// <response code="403">The user lacks permission to cancel room blockouts.</response>
    /// <response code="404">The room blockout does not exist.</response>
    /// <response code="409">The room blockout has already been cancelled.</response>
    /// <response code="500">An internal error prevented cancellation.</response>
    [HttpPost("{id}/cancel")]
    [RequirePermissions("rooms.blockouts.cancel")]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<RoomBlockoutDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Cancel(
        [FromRoute] string id,
        [FromBody] CancelRoomBlockoutDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string cancelledBy = GetAuthenticatedUserId();
        string workerId = $"http-{Guid.NewGuid():N}";
        
        CancelRoomBlockoutCommand command = new CancelRoomBlockoutCommand(RoomBlockoutId: id, CancellationReason: dto.CancellationReason, CancelledBy: cancelledBy);

        Result<RoomBlockoutModel> result = await cancelRoomBlockoutUseCase.ExecuteAsync(inboxId: inboxId, workerId, command, ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<RoomBlockoutDto>(
                    Data: null,
                    Message: "Error cancelling room blockout",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        RoomBlockoutDto roomBlockout =
            RoomBlockoutMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<RoomBlockoutDto>(
                Data: roomBlockout,
                Message: "Room blockout cancelled successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a room blockout by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    /// <param name="id">Identifier of the room blockout to delete.</param>
    /// <param name="inboxId">Inbox identifier supplied through X-Inbox-Id.</param>
    /// <param name="ct">Cancellation token for the HTTP request.</param>
    /// <response code="200">The room blockout was successfully deleted.</response>
    /// <response code="400">The identifier is invalid.</response>
    /// <response code="401">Authentication is required or the JWT is invalid.</response>
    /// <response code="403">The user lacks permission to delete room blockouts.</response>
    /// <response code="404">The room blockout does not exist.</response>
    /// <response code="409">The Inbox operation cannot be processed.</response>
    /// <response code="500">An internal error prevented deletion.</response>
    [HttpDelete("{id}")]
    [RequirePermissions("rooms.blockouts.delete")]
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
            await deleteRoomBlockoutUseCase.ExecuteAsync(
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
                    Message: "Error deleting room blockout",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: "Room blockout deleted",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private string GetAuthenticatedUserId(string? inboxId = null)
    {
        string? userId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = User.FindFirstValue("sub");
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId.Trim();
        }

        throw new UserNotAuthenticatedException(
            text: "The authenticated user ID could not be extracted from the JWT.",
            inboxId: inboxId ?? Request.Headers["X-Inbox-Id"].ToString(),
            traceId: HttpContext.TraceIdentifier,
            path: Request.Path.Value,
            httpMethod: Request.Method,
            endpoint: HttpContext.GetEndpoint()?.DisplayName,
            authenticationScheme: JwtBearerDefaults.AuthenticationScheme,
            requiredClaims:
            [
                JwtRegisteredClaimNames.Sid,
                "sub"
            ],
            metadata: new Dictionary<string, string?>
            {
                ["Controller"] = RouteData.Values["controller"]?.ToString(),
                ["Action"] = RouteData.Values["action"]?.ToString(),
                ["RemoteIpAddress"] = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
    }
}