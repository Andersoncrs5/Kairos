using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Res;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.Authentication;
using Kairos.Api.Config.DB.Transaction;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Commands;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Request;
using Kairos.Api.Modules.Rooms.Equipment.DTOs.Response;
using Kairos.Api.Modules.Rooms.Equipment.Mapper;
using Kairos.Api.Modules.Rooms.Equipment.Model;
using Kairos.Api.Modules.Rooms.Equipment.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kairos.Api.Modules.Rooms.Equipment.Controller;

[ApiController]
[Route("api/v{version:apiVersion}/equipments")]
[ApiVersion("1.0")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class EquipmentController(
    IInboxTransactionUseCase<CreateEquipmentCommand, EquipmentModel, ICreateEquipmentUseCase> createEquipmentUseCase,
    IInboxTransactionUseCase<UpdateEquipmentCommand, EquipmentModel, IUpdateEquipmentUseCase> updateEquipmentUseCase,
    IInboxTransactionUseCase<string, bool, IDeleteEquipmentByIdUseCase> deleteEquipmentUseCase,
    IInboxTransactionUseCase<string, EquipmentModel, IFindEquipmentByIdUseCase> findEquipmentByIdUseCase,
    IInboxTransactionUseCase<string, bool, IExistsEquipmentByIdUseCase> existsEquipmentByIdUseCase,
    IInboxTransactionUseCase<string, bool, IExistsEquipmentByCodeUseCase> existsEquipmentByCodeUseCase,
    IFindAllEquipmentUseCase findAllEquipmentUseCase
) : ControllerBase
{
    // ---------------------------------------------------------------
    // Find all
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves a paginated list of equipments.
    /// </summary>
    /// <remarks>
    /// Supports filtering by ID, version, creation date, update date,
    /// code, name, description and active status.
    /// Supports pagination and sort direction.
    /// Requires a valid JWT and the equipments.read permission.
    /// </remarks>
    [HttpGet]
    [RequirePermissions("equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<Page<EquipmentDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Find(
        [FromQuery] FilterEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        Page<EquipmentModel> result =
            await findAllEquipmentUseCase.ExecuteAsync(dto, ct);

        Page<EquipmentDto> data = Page<EquipmentDto>.Create(
            result.Data
                .Select(EquipmentMapper.ToDto)
                .ToArray(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        return Ok(
            new ResponseHttp<Page<EquipmentDto>>(
                Data: data,
                Message: "Equipments retrieved successfully",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Find by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Retrieves an equipment by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpGet("{id}")]
    [RequirePermissions("equipments.read")]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> FindById(
        [FromRoute] string id,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        Result<EquipmentModel> result =
            await findEquipmentByIdUseCase.ExecuteAsync(
                inboxId,
                workerId,
                id,
                ct);

        if (result.IsFailure)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<EquipmentDto>(
                    Data: null,
                    Message: "Error finding equipment",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        EquipmentDto equipment = EquipmentMapper.ToDto(result.Value);

        return Ok(
            new ResponseHttp<EquipmentDto>(
                Data: equipment,
                Message: "Equipment found",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by ID
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether an equipment exists by its identifier.
    /// </summary>
    [HttpGet("Exists/id/{id}")]
    [RequirePermissions("equipments.read")]
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
            await existsEquipmentByIdUseCase.ExecuteAsync(
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
                    Message: "Error checking equipment ID",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Equipment exists"
                    : "Equipment does not exist",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Exists by Code
    // ---------------------------------------------------------------

    /// <summary>
    /// Checks whether an equipment exists by its code.
    /// </summary>
    [HttpGet("Exists/Code/{code}")]
    [RequirePermissions("equipments.read")]
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
            await existsEquipmentByCodeUseCase.ExecuteAsync(
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
                    Message: "Error checking equipment code",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: result.Value
                    ? "Equipment exists"
                    : "Equipment does not exist",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    /// <summary>
    /// Creates a new equipment.
    /// </summary>
    /// <remarks>
    /// Equipment codes and names must be unique.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPost]
    [RequirePermissions("equipments.create")]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<ValidationErrorResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";
        string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        var command = new CreateEquipmentCommand(dto, userId);

        Result<EquipmentModel> result =
            await createEquipmentUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure || result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<EquipmentDto>(
                    Data: null,
                    Message: "Error creating equipment",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        EquipmentDto equipment = EquipmentMapper.ToDto(result.Value);

        return StatusCode(
            StatusCodes.Status201Created,
            new ResponseHttp<EquipmentDto>(
                Data: equipment,
                Message: "Equipment created",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    /// <summary>
    /// Updates an existing equipment.
    /// </summary>
    /// <remarks>
    /// Equipment codes and names must be unique.
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpPut("{id}")]
    [RequirePermissions("equipments.update")]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ResponseHttp<EquipmentDto>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(
        [FromRoute] string id,
        [FromBody] UpdateEquipmentDto dto,
        [FromHeader(Name = "X-Inbox-Id"), Required] string inboxId,
        CancellationToken ct)
    {
        string workerId = $"http-{Guid.NewGuid():N}";

        var command = new UpdateEquipmentCommand(id, dto);

        Result<EquipmentModel> result =
            await updateEquipmentUseCase.ExecuteAsync(
                inboxId,
                workerId,
                command,
                ct);

        if (result.IsFailure && result.Value is null)
        {
            return StatusCode(
                result.StatusCode,
                new ResponseHttp<EquipmentDto>(
                    Data: null,
                    Message: "Error updating equipment",
                    TraceId: HttpContext.TraceIdentifier,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        EquipmentDto equipment = EquipmentMapper.ToDto(result.Value!);

        return Ok(
            new ResponseHttp<EquipmentDto>(
                Data: equipment,
                Message: "Equipment updated successfully",
                TraceId: HttpContext.TraceIdentifier,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }

    // ---------------------------------------------------------------
    // Delete
    // ---------------------------------------------------------------

    /// <summary>
    /// Deletes an equipment by its identifier.
    /// </summary>
    /// <remarks>
    /// Requires a valid JWT and the X-Inbox-Id header.
    /// The operation is processed through the Inbox transaction service.
    /// </remarks>
    [HttpDelete("{id}")]
    [RequirePermissions("equipments.delete")]
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
            await deleteEquipmentUseCase.ExecuteAsync(
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
                    Message: "Error deleting equipment",
                    TraceId: inboxId,
                    Success: false,
                    Timestamp: DateTimeOffset.UtcNow,
                    Errors: result.Errors));
        }

        return Ok(
            new ResponseHttp<bool?>(
                Data: result.Value,
                Message: "Equipment deleted",
                TraceId: inboxId,
                Success: true,
                Timestamp: DateTimeOffset.UtcNow));
    }
}