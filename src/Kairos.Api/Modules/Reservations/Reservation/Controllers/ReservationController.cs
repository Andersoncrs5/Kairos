using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Asp.Versioning;
using Kairos.Api.Common.Exceptions;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Commands;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Request;
using Kairos.Api.Modules.Reservations.Reservation.DTOs.Response;
using Kairos.Api.Modules.Reservations.Reservation.Mapper;
using Kairos.Api.Modules.Reservations.Reservation.Models;
using Kairos.Api.Modules.Reservations.Reservation.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Reservations.Reservation.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/reservations")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class ReservationController(
    IInboxTransactionUseCase<CreateReservationCommand, ReservationModel, ICreateReservationUseCase> createReservationUseCase,
    IInboxTransactionUseCase<UpdateReservationCommand, ReservationModel, IUpdateReservationUseCase> updateReservationUseCase,
    IInboxTransactionUseCase<CancelReservationCommand, ReservationModel, ICancelReservationUseCase> cancelReservationUseCase,
    IInboxTransactionUseCase<string, bool, IDeleteReservationByIdUseCase> deleteReservationUseCase,
    IInboxTransactionUseCase<string, bool, IExistsReservationByIdUseCase> existsReservationByIdUseCase,
    IInboxTransactionUseCase<string, ReservationModel, IFindReservationByIdUseCase> findReservationByIdUseCase,
    IInboxTransactionUseCase<FilterReservationDto, Page<ReservationModel>, IFindReservationsUseCase> findReservationsUseCase,
    IInboxTransactionUseCase<ReservationRoomConflictCommand, bool, IExistsConflictingByRoomUseCase> existsConflictingByRoomUseCase,
    IInboxTransactionUseCase<ReservationRoomConflictCommand, IReadOnlyList<ReservationModel>, IGetConflictingByRoomUseCase> getConflictingByRoomUseCase
) : ControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of reservations.
    /// </summary>
    /// <remarks>
    /// Supports filtering, pagination and sorting.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("reservations.list")]
    [ProducesResponseType(typeof(ResponseHttp<Page<ReservationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterReservationDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<Page<ReservationModel>> result =
            await findReservationsUseCase.ExecuteAsync(
                inboxId,
                workerId,
                dto,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<Page<ReservationDto>>(
                    Data: null,
                    Message: "Error retrieving reservations",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        Page<ReservationDto> data =
            Page<ReservationDto>.Create(
                result.Value.Data
                    .Select(ReservationMapper.ToDto)
                    .ToArray(),
                result.Value.TotalCount,
                result.Value.PageNumber,
                result.Value.PageSize);

        return Ok(
            new ResponseHttp<Page<ReservationDto>>(
                Data: data,
                Message: "Reservations retrieved successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a reservation by its identifier.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermissions("reservations.read")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationDto>), StatusCodes.Status200OK)]
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

        Result<ReservationModel> result =
            await findReservationByIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<ReservationDto>(
                    Data: null,
                    Message: "Error finding reservation",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        ReservationDto reservation =
            ReservationMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<ReservationDto>(
                Data: reservation,
                Message: "Reservation found",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a reservation exists by its identifier.
    /// </summary>
    [HttpGet("exists/id/{id}")]
    [RequirePermissions("reservations.read")]
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
            await existsReservationByIdUseCase.ExecuteAsync(inboxId, workerId, id, ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking reservation ID",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Reservation exists"
                    : "Reservation does not exist",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists conflicting by room
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether a room has conflicting reservations.
    /// </summary>
    [HttpGet("rooms/{roomId}/conflicts/exists")]
    [RequirePermissions("rooms.availability.read")]
    [ProducesResponseType(typeof(ResponseHttp<bool?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExistsConflictingByRoom(
        [FromRoute, Required] string roomId,
        [FromQuery, Required] DateTimeOffset startsAt,
        [FromQuery, Required] DateTimeOffset endsAt,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        ReservationRoomConflictCommand command = new(
            roomId,
            startsAt,
            endsAt);

        Result<bool> result =
            await existsConflictingByRoomUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<bool?>(
                    Data: null,
                    Message: "Error checking reservation conflicts",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Room has conflicting reservations"
                    : "Room has no conflicting reservations",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Get conflicting by room
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves reservations conflicting with a room and time range.
    /// </summary>
    [HttpGet("rooms/{roomId}/conflicts")]
    [RequirePermissions("rooms.availability.read")]
    [ProducesResponseType(typeof(ResponseHttp<IReadOnlyList<ReservationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetConflictingByRoom(
        [FromRoute, Required] string roomId,
        [FromQuery, Required] DateTimeOffset startsAt,
        [FromQuery, Required] DateTimeOffset endsAt,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        ReservationRoomConflictCommand command = new(
            roomId,
            startsAt,
            endsAt);

        Result<IReadOnlyList<ReservationModel>> result =
            await getConflictingByRoomUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<IReadOnlyList<ReservationDto>>(
                    Data: null,
                    Message: "Error retrieving conflicting reservations",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        IReadOnlyList<ReservationDto> data =
            result.Value
                .Select(ReservationMapper.ToDto)
                .ToArray();

        return Ok(
            new ResponseHttp<IReadOnlyList<ReservationDto>>(
                Data: data,
                Message: "Conflicting reservations retrieved successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new reservation.
    /// </summary>
    /// <remarks>
    /// The reservation owner is identified through the authenticated JWT.
    /// Requires the X-Inbox-Id header.
    /// </remarks>
    [HttpPost]
    [RequirePermissions("reservations.create")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReservationDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string userId = GetAuthenticatedUserId(inboxId);
        string workerId = $"http-{Guid.NewGuid():N}";

        CreateReservationCommand command = new()
        {
            Dto = dto,
            UserId = userId
        };

        Result<ReservationModel> result =
            await createReservationUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<ReservationDto>(
                    Data: null,
                    Message: "Error creating reservation",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        ReservationDto reservation =
            ReservationMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<ReservationDto>(
                Data: reservation,
                Message: "Reservation created successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing reservation.
    /// </summary>
    /// <remarks>
    /// Only the supplied properties are changed.
    /// Room, category and reservation period changes are validated.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("reservations.update")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute, Required] string id,
        [FromBody] UpdateReservationDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        UpdateReservationCommand command = new(id, dto);

        Result<ReservationModel> result =
            await updateReservationUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<ReservationDto>(
                    Data: null,
                    Message: "Error updating reservation",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        ReservationDto reservation =
            ReservationMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<ReservationDto>(
                Data: reservation,
                Message: "Reservation updated successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Cancel
    // ---------------------------------------------------------------

    /// <summary>
    /// Cancels an existing reservation.
    /// </summary>
    /// <remarks>
    /// The cancellation actor is identified through the authenticated JWT.
    /// </remarks>
    [HttpPost("{id}/cancel")]
    [RequirePermissions("reservations.cancel")]
    [ProducesResponseType(typeof(ResponseHttp<ReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Cancel(
        [FromRoute, Required] string id,
        [FromBody] CancelReservationDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string cancelledBy = GetAuthenticatedUserId(inboxId);
        string workerId = $"http-{Guid.NewGuid():N}";

        CancelReservationCommand command = new(
            ReservationId: id,
            UserId: cancelledBy,
            Dto: dto);

        Result<ReservationModel> result =
            await cancelReservationUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<ReservationDto>(
                    Data: null,
                    Message: "Error cancelling reservation",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        ReservationDto reservation =
            ReservationMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<ReservationDto>(
                Data: reservation,
                Message: "Reservation cancelled successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes a reservation by its identifier.
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermissions("reservations.delete")]
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
            await deleteReservationUseCase.ExecuteAsync(
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
                    Message: "Error deleting reservation",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Reservation deleted successfully"
                    : "Reservation was not found",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private string GetAuthenticatedUserId(string? inboxId = null)
    {
        string? userId =
            User.FindFirstValue(JwtRegisteredClaimNames.Sid);

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
                ["RemoteIpAddress"] =
                    HttpContext.Connection.RemoteIpAddress?.ToString()
            });
    }
}